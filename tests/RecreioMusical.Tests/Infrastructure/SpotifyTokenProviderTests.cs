using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Infrastructure;

public sealed class SpotifyTokenProviderTests
{
    private const string TokenJson = """{ "access_token": "token-abc", "expires_in": 3600 }""";

    [Fact]
    public async Task Throws_without_credentials_and_never_calls_http()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json(TokenJson),
            new SpotifyOptions());

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Provider.GetAccessTokenAsync(CancellationToken.None));

        Assert.Empty(sut.Handler.Requests);
    }

    [Fact]
    public async Task Requests_a_client_credentials_token_with_basic_auth()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json(TokenJson),
            Configured());

        var token = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-abc", token);

        var request = Assert.Single(sut.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://accounts.spotify.com/api/token", request.Url);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("client-id:client-secret")),
            request.Authorization);
        Assert.Equal("grant_type=client_credentials", request.Body);
    }

    [Fact]
    public async Task Reuses_the_token_until_it_is_near_expiry()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json(TokenJson),
            Configured());

        var first = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(sut.Handler.Requests);

        sut.Time.UtcNow = sut.Time.UtcNow.AddSeconds(3600 - 60);

        var renewed = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-abc", renewed);
        Assert.Equal(2, sut.Handler.Requests.Count);
    }

    [Fact]
    public async Task Invalidate_forces_a_new_token()
    {
        using var sut = CreateSut(
            (_, attempt) => StubHttpMessageHandler.Json(
                attempt == 0
                    ? TokenJson
                    : """{ "access_token": "token-novo", "expires_in": 3600 }"""),
            Configured());

        var first = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);
        sut.Provider.Invalidate(first);
        var second = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-abc", first);
        Assert.Equal("token-novo", second);
        Assert.Equal(2, sut.Handler.Requests.Count);
    }

    [Fact]
    public async Task Invalidate_ignores_a_different_token()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json(TokenJson),
            Configured());

        var token = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);
        sut.Provider.Invalidate("outro-token");
        var again = await sut.Provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(token, again);
        Assert.Single(sut.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Throws_when_the_token_endpoint_fails(HttpStatusCode statusCode)
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Status(statusCode),
            Configured());

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Provider.GetAccessTokenAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("""{ "access_token": "", "expires_in": 3600 }""")]
    [InlineData("""{ "access_token": "abc", "expires_in": 0 }""")]
    [InlineData("{ not-json")]
    public async Task Throws_on_an_invalid_payload(string json)
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json(json),
            Configured());

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Provider.GetAccessTokenAsync(CancellationToken.None));
    }

    private static SpotifyOptions Configured() => new()
    {
        ClientId = "client-id",
        ClientSecret = "client-secret"
    };

    private static TokenSut CreateSut(
        Func<CapturedRequest, int, HttpResponseMessage> responder,
        SpotifyOptions options)
    {
        var handler = new StubHttpMessageHandler(responder);
        var factory = new StubHttpClientFactory(handler);
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));

        var provider = new SpotifyTokenProvider(
            factory,
            Options.Create(options),
            time);

        return new TokenSut(provider, handler, factory, time);
    }

    private sealed class TokenSut : IDisposable
    {
        private readonly StubHttpClientFactory _factory;

        public TokenSut(
            SpotifyTokenProvider provider,
            StubHttpMessageHandler handler,
            StubHttpClientFactory factory,
            FixedTimeProvider time)
        {
            Provider = provider;
            Handler = handler;
            _factory = factory;
            Time = time;
        }

        public SpotifyTokenProvider Provider { get; }

        public StubHttpMessageHandler Handler { get; }

        public FixedTimeProvider Time { get; }

        public void Dispose() => _factory.Dispose();
    }
}
