using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace RecreioMusical.Tests;

public sealed class ProductionReadinessTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _databasePath;

    public ProductionReadinessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"recreio-musical-production-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task Health_endpoint_reports_database_connection()
    {
        using var factory = CreateFactory(_databasePath);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_opt_out_of_search_indexing()
    {
        using var factory = CreateFactory(_databasePath);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.True(
            response.Headers.TryGetValues("X-Robots-Tag", out var values),
            "O cabeçalho X-Robots-Tag não foi enviado.");
        Assert.Contains("noindex", string.Join(",", values!), StringComparison.OrdinalIgnoreCase);

        using var robots = await client.GetAsync("/robots.txt");
        robots.EnsureSuccessStatusCode();
        Assert.Contains(
            "Disallow: /",
            await robots.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_creates_missing_database_directory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"recreio-musical-directory-tests-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "data", "playlist.db");

        try
        {
            using var factory = CreateFactory(databasePath);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(File.Exists(databasePath), "O arquivo SQLite não foi criado.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    public void Dispose()
    {
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

    private WebApplicationFactory<Program> CreateFactory(string databasePath) =>
        _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "ConnectionStrings:Playlist",
                $"Data Source={databasePath};Pooling=False"));
}
