using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RecreioMusical.Application.Abstractions;

namespace RecreioMusical.Infrastructure.Spotify;

/// <summary>Cliente HTTP compartilhado pelas chamadas ao Spotify.</summary>
public static class SpotifyHttpClient
{
    public const string Name = "Spotify";
}

/// <summary>Fornece um access token válido para a API do Spotify.</summary>
public interface ISpotifyAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    /// <summary>Descarta o token informado caso ele tenha sido rejeitado pela API.</summary>
    void Invalidate(string accessToken);
}

/// <summary>
/// Implementa o fluxo Client Credentials: o app se autentica como ele mesmo, sem login
/// do usuário, e reaproveita o token até pouco antes de expirar.
/// </summary>
public sealed class SpotifyTokenProvider : ISpotifyAccessTokenProvider
{
    private static readonly Uri TokenEndpoint = new("https://accounts.spotify.com/api/token");
    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<SpotifyOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _stateLock = new();

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public SpotifyTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<SpotifyOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.IsConfigured)
        {
            throw new SpotifyUnavailableException(
                "Spotify não configurado: defina ClientId e ClientSecret.");
        }

        lock (_stateLock)
        {
            if (IsTokenValid())
            {
                return _accessToken!;
            }
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            lock (_stateLock)
            {
                if (IsTokenValid())
                {
                    return _accessToken!;
                }
            }

            var (token, expiresIn) = await RequestTokenAsync(options, cancellationToken);

            lock (_stateLock)
            {
                _accessToken = token;
                _expiresAt = _timeProvider.GetUtcNow() + CalculateLifetime(expiresIn);
            }

            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate(string accessToken)
    {
        lock (_stateLock)
        {
            if (string.Equals(_accessToken, accessToken, StringComparison.Ordinal))
            {
                _accessToken = null;
                _expiresAt = default;
            }
        }
    }

    private bool IsTokenValid() =>
        _accessToken is not null && _timeProvider.GetUtcNow() < _expiresAt;

    private static TimeSpan CalculateLifetime(int expiresInSeconds)
    {
        var lifetime = TimeSpan.FromSeconds(Math.Max(1, expiresInSeconds));
        return lifetime > ExpirySafetyMargin ? lifetime - ExpirySafetyMargin : lifetime;
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(
        SpotifyOptions options,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        ]);

        var client = _httpClientFactory.CreateClient(SpotifyHttpClient.Name);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new SpotifyUnavailableException(
                "Não foi possível falar com o Spotify.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpotifyUnavailableException(
                "O Spotify demorou demais para responder.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new SpotifyUnavailableException(
                    $"O Spotify recusou a autenticação ({(int)response.StatusCode}).");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<SpotifyTokenResponse>(
                    stream,
                    cancellationToken: cancellationToken);

                if (payload is null ||
                    string.IsNullOrWhiteSpace(payload.AccessToken) ||
                    payload.ExpiresIn <= 0)
                {
                    throw new SpotifyUnavailableException(
                        "O Spotify devolveu uma autenticação inválida.");
                }

                return (payload.AccessToken, payload.ExpiresIn);
            }
            catch (JsonException exception)
            {
                throw new SpotifyUnavailableException(
                    "O Spotify devolveu uma autenticação inválida.",
                    exception);
            }
        }
    }

    private sealed class SpotifyTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
