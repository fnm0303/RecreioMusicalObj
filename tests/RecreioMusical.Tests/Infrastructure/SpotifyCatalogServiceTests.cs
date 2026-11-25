using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Infrastructure.Spotify;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Infrastructure;

public sealed class SpotifyCatalogServiceTests
{
    private const string TrackId = "4uLU6hMCjMI75M1A2tKUQC";

    private const string SearchJson = """
        {
          "tracks": {
            "items": [
              {
                "id": "4uLU6hMCjMI75M1A2tKUQC",
                "name": "Never Gonna Give You Up",
                "artists": [{ "name": "Rick Astley" }],
                "album": {
                  "name": "Whenever You Need Somebody",
                  "images": [
                    { "url": "https://i.scdn.co/image/64", "width": 64 },
                    { "url": "https://i.scdn.co/image/300", "width": 300 },
                    { "url": "https://i.scdn.co/image/640", "width": 640 }
                  ]
                },
                "duration_ms": 213573,
                "external_urls": { "spotify": "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC" }
              },
              {
                "id": "4uLU6hMCjMI75M1A2tKUQC",
                "name": "Never Gonna Give You Up (repetida)",
                "artists": [{ "name": "Rick Astley" }],
                "duration_ms": 213573
              },
              {
                "id": "0000000000000000000002",
                "name": "Sem artistas",
                "artists": [],
                "duration_ms": 1000
              },
              {
                "id": "0000000000000000000003",
                "name": "Sem link externo",
                "artists": [{ "name": "Daft Punk" }, { "name": "Pharrell Williams" }],
                "album": { "name": "Random Access Memories", "images": [] },
                "duration_ms": 369600
              }
            ]
          }
        }
        """;

    private const string TrackJson = """
        {
          "id": "4uLU6hMCjMI75M1A2tKUQC",
          "name": "Never Gonna Give You Up",
          "artists": [{ "name": "Rick Astley" }],
          "album": {
            "name": "Whenever You Need Somebody",
            "images": [{ "url": "https://i.scdn.co/image/300", "width": 300 }]
          },
          "duration_ms": 213573,
          "external_urls": { "spotify": "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC" }
        }
        """;

    [Fact]
    public async Task Search_maps_tracks_and_sends_market_and_limit()
    {
        using var sut = CreateSut((_, _) => StubHttpMessageHandler.Json(SearchJson));

        var tracks = await sut.Service.SearchTracksAsync("daft punk", CancellationToken.None);

        var request = Assert.Single(sut.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.NotNull(request.Url);
        Assert.Contains("type=track", request.Url, StringComparison.Ordinal);
        Assert.Contains("limit=7", request.Url, StringComparison.Ordinal);
        Assert.Contains("market=BR", request.Url, StringComparison.Ordinal);
        Assert.Contains("q=daft%20punk", request.Url, StringComparison.Ordinal);
        Assert.Equal("Bearer token-1", request.Authorization);

        Assert.Equal(2, tracks.Count);

        var first = tracks[0];
        Assert.Equal(TrackId, first.Id);
        Assert.Equal("Never Gonna Give You Up", first.Title);
        Assert.Equal("Rick Astley", first.Artists);
        Assert.Equal("Whenever You Need Somebody", first.AlbumName);
        Assert.Equal("https://i.scdn.co/image/300", first.CoverUrl);
        Assert.Equal(213_573, first.DurationMs);
        Assert.Equal($"https://open.spotify.com/track/{TrackId}", first.SpotifyUrl);

        var second = tracks[1];
        Assert.Equal("Daft Punk, Pharrell Williams", second.Artists);
        Assert.Null(second.CoverUrl);
        Assert.Equal(
            "https://open.spotify.com/track/0000000000000000000003",
            second.SpotifyUrl);
    }

    [Fact]
    public async Task Search_returns_empty_when_spotify_has_no_items()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json("""{ "tracks": { "items": [] } }"""));

        var tracks = await sut.Service.SearchTracksAsync("nada", CancellationToken.None);

        Assert.Empty(tracks);
    }

    [Fact]
    public async Task Search_retries_once_after_a_401_with_a_new_token()
    {
        using var sut = CreateSut((_, attempt) => attempt == 0
            ? StubHttpMessageHandler.Status(HttpStatusCode.Unauthorized)
            : StubHttpMessageHandler.Json(SearchJson));

        var tracks = await sut.Service.SearchTracksAsync("daft punk", CancellationToken.None);

        Assert.NotEmpty(tracks);
        Assert.Equal(2, sut.Handler.Requests.Count);
        Assert.Equal("Bearer token-1", sut.Handler.Requests[0].Authorization);
        Assert.Equal("Bearer token-refreshed", sut.Handler.Requests[1].Authorization);
        Assert.Equal(new[] { "token-1" }, sut.Tokens.InvalidatedTokens);
    }

    [Fact]
    public async Task Search_retries_once_after_a_429_honouring_retry_after()
    {
        using var sut = CreateSut((_, attempt) =>
        {
            if (attempt > 0)
            {
                return StubHttpMessageHandler.Json(SearchJson);
            }

            var response = StubHttpMessageHandler.Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter =
                new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
            return response;
        });

        var tracks = await sut.Service.SearchTracksAsync("daft punk", CancellationToken.None);

        Assert.NotEmpty(tracks);
        Assert.Equal(2, sut.Handler.Requests.Count);
    }

    [Fact]
    public async Task Search_throws_when_spotify_keeps_rate_limiting()
    {
        using var sut = CreateSut((_, _) =>
        {
            var response = StubHttpMessageHandler.Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter =
                new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
            return response;
        });

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Service.SearchTracksAsync("daft punk", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Search_throws_when_spotify_fails(HttpStatusCode statusCode)
    {
        using var sut = CreateSut((_, _) => StubHttpMessageHandler.Status(statusCode));

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Service.SearchTracksAsync("daft punk", CancellationToken.None));
    }

    [Fact]
    public async Task Search_throws_on_invalid_json()
    {
        using var sut = CreateSut((_, _) => StubHttpMessageHandler.Json("{ not-json"));

        await Assert.ThrowsAsync<SpotifyUnavailableException>(
            () => sut.Service.SearchTracksAsync("daft punk", CancellationToken.None));
    }

    [Fact]
    public async Task GetTrack_maps_the_track()
    {
        using var sut = CreateSut((_, _) => StubHttpMessageHandler.Json(TrackJson));

        var track = await sut.Service.GetTrackAsync(TrackId, CancellationToken.None);

        Assert.NotNull(track);
        Assert.Equal(TrackId, track.Id);
        Assert.Equal("Never Gonna Give You Up", track.Title);
        Assert.Equal("Rick Astley", track.Artists);

        var request = Assert.Single(sut.Handler.Requests);
        Assert.Equal(
            $"https://api.spotify.com/v1/tracks/{TrackId}?market=BR",
            request.Url);
    }

    [Fact]
    public async Task GetTrack_returns_null_when_spotify_does_not_find_the_track()
    {
        using var sut = CreateSut((_, _) => StubHttpMessageHandler.Status(HttpStatusCode.NotFound));

        var track = await sut.Service.GetTrackAsync(TrackId, CancellationToken.None);

        Assert.Null(track);
    }

    [Fact]
    public async Task GetTrack_returns_null_for_malformed_track_payload()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json("""{ "id": null, "name": null }"""));

        var track = await sut.Service.GetTrackAsync(TrackId, CancellationToken.None);

        Assert.Null(track);
    }

    [Fact]
    public async Task Uses_br_as_fallback_market_and_clamps_the_limit()
    {
        using var sut = CreateSut(
            (_, _) => StubHttpMessageHandler.Json("""{ "tracks": { "items": [] } }"""),
            new SpotifyOptions { Market = "  ", SearchLimit = 500 });

        await sut.Service.SearchTracksAsync("teste", CancellationToken.None);

        var url = Assert.Single(sut.Handler.Requests).Url;
        Assert.NotNull(url);
        Assert.Contains("market=BR", url, StringComparison.Ordinal);
        Assert.Contains("limit=20", url, StringComparison.Ordinal);
    }

    private static CatalogSut CreateSut(
        Func<CapturedRequest, int, HttpResponseMessage> responder,
        SpotifyOptions? options = null)
    {
        var handler = new StubHttpMessageHandler(responder);
        var factory = new StubHttpClientFactory(handler);
        var tokens = new FakeSpotifyTokenProvider();

        var service = new SpotifyCatalogService(
            factory,
            tokens,
            Options.Create(options ?? new SpotifyOptions
            {
                Market = "br",
                SearchLimit = 7
            }),
            NullLogger<SpotifyCatalogService>.Instance);

        return new CatalogSut(service, handler, tokens, factory);
    }

    private sealed class CatalogSut : IDisposable
    {
        private readonly StubHttpClientFactory _factory;

        public CatalogSut(
            SpotifyCatalogService service,
            StubHttpMessageHandler handler,
            FakeSpotifyTokenProvider tokens,
            StubHttpClientFactory factory)
        {
            Service = service;
            Handler = handler;
            Tokens = tokens;
            _factory = factory;
        }

        public SpotifyCatalogService Service { get; }

        public StubHttpMessageHandler Handler { get; }

        public FakeSpotifyTokenProvider Tokens { get; }

        public void Dispose() => _factory.Dispose();
    }
}
