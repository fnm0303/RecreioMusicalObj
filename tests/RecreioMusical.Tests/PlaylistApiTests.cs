using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;
using RecreioMusical.Domain;

namespace RecreioMusical.Tests;

public sealed class PlaylistApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string AdminPin = "2468";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _databasePath;
    private readonly List<IDisposable> _disposables = [];

    public PlaylistApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"recreio-musical-api-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task Get_playlist_starts_empty_with_five_slots()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/playlist");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json.GetProperty("count").GetInt32());
        Assert.Equal(PlaylistRules.Capacity, json.GetProperty("capacity").GetInt32());
        Assert.False(json.GetProperty("isFull").GetBoolean());
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Post_adds_track_with_nickname()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);

        using var response = await client.PostAsJsonAsync(
            "/api/playlist",
            new { trackId = TestData.DefaultTrackId, addedBy = "Ana" });
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, json.GetProperty("playlist").GetProperty("count").GetInt32());

        var item = json.GetProperty("item");
        Assert.Equal(1, item.GetProperty("slot").GetInt32());
        Assert.Equal(TestData.DefaultTrackId, item.GetProperty("trackId").GetString());
        Assert.Equal("Ana", item.GetProperty("addedBy").GetString());
    }

    [Fact]
    public async Task Post_rejects_invalid_nickname()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);

        using var response = await client.PostAsJsonAsync(
            "/api/playlist",
            new { trackId = TestData.DefaultTrackId, addedBy = "A" });
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlaylistErrors.InvalidNickname, json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_rejects_duplicate_track()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);

        var body = new { trackId = TestData.DefaultTrackId, addedBy = "Ana" };

        using var first = await client.PostAsJsonAsync("/api/playlist", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/playlist", body);
        var json = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("duplicate_track", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_rejects_the_sixth_track()
    {
        var tracks = Enumerable.Range(1, 6)
            .Select(index => TestData.Track(TestData.TrackId(index), $"Música {index}"))
            .ToArray();
        var catalog = new FakeSpotifyCatalog(tracks);
        using var client = CreateClient(catalog);

        for (var index = 0; index < PlaylistRules.Capacity; index++)
        {
            using var added = await client.PostAsJsonAsync(
                "/api/playlist",
                new { trackId = tracks[index].Id, addedBy = "Ana" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        using var response = await client.PostAsJsonAsync(
            "/api/playlist",
            new { trackId = tracks[5].Id, addedBy = "Bia" });
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("playlist_full", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_without_pin_is_unauthorized()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);
        await AddDefaultTrackAsync(client);

        using var response = await client.DeleteAsync("/api/playlist/1");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_pin", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_with_valid_pin_removes_track()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);
        await AddDefaultTrackAsync(client);

        using var response = await DeleteWithPinAsync(client, "/api/playlist/1");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var playlist = await GetPlaylistAsync(client);
        Assert.Equal(0, playlist.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Delete_with_wrong_pin_is_unauthorized()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);
        await AddDefaultTrackAsync(client);

        using var response = await DeleteWithPinAsync(client, "/api/playlist/1", "0000");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_pin", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Clear_with_valid_pin_empties_playlist()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);
        await AddDefaultTrackAsync(client);

        using var response = await DeleteWithPinAsync(client, "/api/playlist");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var playlist = await GetPlaylistAsync(client);
        Assert.Equal(0, playlist.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Repeated_wrong_pins_are_rate_limited()
    {
        using var client = CreateClient(new FakeSpotifyCatalog(TestData.Track()));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await DeleteWithPinAsync(client, "/api/playlist", "0000");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var blocked = await DeleteWithPinAsync(client, "/api/playlist", "0000");
        var json = await ReadJsonAsync(blocked);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("too_many_attempts", json.GetProperty("code").GetString());
        Assert.True(blocked.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Search_returns_catalog_tracks()
    {
        var catalog = new FakeSpotifyCatalog(TestData.Track());
        using var client = CreateClient(catalog);

        using var response = await client.GetAsync("/api/tracks/search?q=rick");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.GetProperty("tracks").GetArrayLength());
        Assert.Equal("rick", catalog.LastQuery);
    }

    [Fact]
    public async Task Search_rejects_short_query()
    {
        using var client = CreateClient(new FakeSpotifyCatalog());

        using var response = await client.GetAsync("/api/tracks/search?q=a");
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_search_query", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Events_stream_sends_initial_playlist_snapshot()
    {
        using var client = CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        using var response = await client.GetAsync(
            "/api/playlist/events",
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var buffer = new byte[2048];
        var payload = new StringBuilder();

        while (!payload.ToString().Contains("event: playlist", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            if (read == 0)
            {
                break;
            }

            payload.Append(Encoding.UTF8.GetString(buffer, 0, read));
        }

        var text = payload.ToString();
        Assert.Contains("event: playlist", text, StringComparison.Ordinal);
        Assert.Contains("\"capacity\":5", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private HttpClient CreateClient(FakeSpotifyCatalog? catalog = null)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Playlist",
                $"Data Source={_databasePath};Pooling=False");
            builder.UseSetting("Admin:Pin", AdminPin);

            if (catalog is not null)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISpotifyCatalogService>();
                    services.AddSingleton<ISpotifyCatalogService>(catalog);
                });
            }
        });

        _disposables.Add(factory);
        return factory.CreateClient();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> DeleteWithPinAsync(
        HttpClient client,
        string path,
        string pin = AdminPin)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Add("X-Admin-Pin", pin);
        return client.SendAsync(request);
    }

    private static async Task AddDefaultTrackAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/playlist",
            new { trackId = TestData.DefaultTrackId, addedBy = "Ana" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> GetPlaylistAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/playlist");
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private sealed class FakeSpotifyCatalog : ISpotifyCatalogService
    {
        private readonly Dictionary<string, TrackDto> _tracks;

        public FakeSpotifyCatalog(params TrackDto[] tracks)
        {
            _tracks = tracks.ToDictionary(track => track.Id, StringComparer.Ordinal);
        }

        public string? LastQuery { get; private set; }

        public Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
            string query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<TrackDto>>(_tracks.Values.ToArray());
        }

        public Task<TrackDto?> GetTrackAsync(string trackId, CancellationToken cancellationToken) =>
            Task.FromResult(_tracks.GetValueOrDefault(trackId));
    }
}
