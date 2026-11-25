using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Infrastructure.Spotify;

/// <summary>
/// Catálogo do Spotify via Web API. Roda só no servidor: as credenciais nunca saem
/// daqui. Um 401 renova o token e um 429 respeita o <c>Retry-After</c>, ambos com uma
/// única nova tentativa.
/// </summary>
public sealed class SpotifyCatalogService : ISpotifyCatalogService
{
    private const string SearchEndpoint = "https://api.spotify.com/v1/search";
    private const string TracksEndpoint = "https://api.spotify.com/v1/tracks";
    private const int MaxAttempts = 2;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISpotifyAccessTokenProvider _tokens;
    private readonly IOptions<SpotifyOptions> _options;
    private readonly ILogger<SpotifyCatalogService> _logger;

    public SpotifyCatalogService(
        IHttpClientFactory httpClientFactory,
        ISpotifyAccessTokenProvider tokens,
        IOptions<SpotifyOptions> options,
        ILogger<SpotifyCatalogService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _tokens = tokens;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var limit = Math.Clamp(options.SearchLimit, 1, 20);
        var market = Uri.EscapeDataString(NormalizeMarket(options.Market));
        var url =
            $"{SearchEndpoint}?type=track&limit={limit}&market={market}&q={Uri.EscapeDataString(query)}";

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            cancellationToken);

        EnsureSuccess(response, "buscar músicas");

        var payload = await ReadJsonAsync<SpotifySearchResponse>(response, cancellationToken);
        var items = payload?.Tracks?.Items;
        if (items is null || items.Count == 0)
        {
            return [];
        }

        var results = new List<TrackDto>(items.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var track = Map(item);
            if (track is not null && seen.Add(track.Id))
            {
                results.Add(track);
            }
        }

        return results;
    }

    public async Task<TrackDto?> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken)
    {
        var market = Uri.EscapeDataString(NormalizeMarket(_options.Value.Market));
        var url = $"{TracksEndpoint}/{Uri.EscapeDataString(trackId)}?market={market}";

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, "consultar a música");

        var payload = await ReadJsonAsync<SpotifyTrack>(response, cancellationToken);
        return Map(payload);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var token = await _tokens.GetAccessTokenAsync(cancellationToken);

            using var request = requestFactory();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt < MaxAttempts)
            {
                _tokens.Invalidate(token);
                response.Dispose();
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(response);
                response.Dispose();

                if (delay > MaxRetryDelay)
                {
                    throw new SpotifyUnavailableException(
                        "O Spotify pediu uma pausa antes da próxima tentativa.");
                }

                await Task.Delay(delay, cancellationToken);
                continue;
            }

            return response;
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _logger.LogWarning(
            "Spotify respondeu {StatusCode} ao tentar {Operation}.",
            (int)response.StatusCode,
            operation);

        throw new SpotifyUnavailableException(
            $"O Spotify não conseguiu {operation} ({(int)response.StatusCode}).");
    }

    private static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new SpotifyUnavailableException(
                "O Spotify devolveu uma resposta inesperada.",
                exception);
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                return wait;
            }
        }

        return TimeSpan.FromSeconds(1);
    }

    private static string NormalizeMarket(string? market) =>
        string.IsNullOrWhiteSpace(market) ? "BR" : market.Trim().ToUpperInvariant();

    private static TrackDto? Map(SpotifyTrack? track)
    {
        if (track is null ||
            string.IsNullOrWhiteSpace(track.Id) ||
            string.IsNullOrWhiteSpace(track.Name))
        {
            return null;
        }

        var artists = string.Join(
            ", ",
            (track.Artists ?? [])
                .Select(artist => artist.Name?.Trim())
                .Where(name => !string.IsNullOrEmpty(name)));

        if (artists.Length == 0)
        {
            return null;
        }

        var spotifyUrl = track.ExternalUrls is not null &&
                         track.ExternalUrls.TryGetValue("spotify", out var externalUrl) &&
                         !string.IsNullOrWhiteSpace(externalUrl)
            ? externalUrl
            : $"https://open.spotify.com/track/{track.Id}";

        return new TrackDto(
            track.Id.Trim(),
            track.Name.Trim(),
            artists,
            track.Album?.Name?.Trim() ?? string.Empty,
            PickCover(track.Album),
            Math.Max(0, track.DurationMs),
            spotifyUrl.Trim());
    }

    private static string? PickCover(SpotifyAlbum? album)
    {
        var images = album?.Images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .ToArray();

        if (images is null || images.Length == 0)
        {
            return null;
        }

        var preferred = images
            .Where(image => image.Width is >= 200 and <= 400)
            .OrderByDescending(image => image.Width)
            .FirstOrDefault();

        return (preferred ?? images.OrderByDescending(image => image.Width ?? 0).First()).Url;
    }

    private sealed class SpotifySearchResponse
    {
        public SpotifyTracksPage? Tracks { get; init; }
    }

    private sealed class SpotifyTracksPage
    {
        public List<SpotifyTrack?>? Items { get; init; }
    }

    private sealed class SpotifyTrack
    {
        public string? Id { get; init; }

        public string? Name { get; init; }

        public List<SpotifyArtist>? Artists { get; init; }

        public SpotifyAlbum? Album { get; init; }

        [JsonPropertyName("duration_ms")]
        public int DurationMs { get; init; }

        [JsonPropertyName("external_urls")]
        public Dictionary<string, string>? ExternalUrls { get; init; }
    }

    private sealed class SpotifyArtist
    {
        public string? Name { get; init; }
    }

    private sealed class SpotifyAlbum
    {
        public string? Name { get; init; }

        public List<SpotifyImage>? Images { get; init; }
    }

    private sealed class SpotifyImage
    {
        public string? Url { get; init; }

        public int? Width { get; init; }
    }
}
