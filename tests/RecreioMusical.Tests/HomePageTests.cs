using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace RecreioMusical.Tests;

public sealed class HomePageTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _databasePath;

    public HomePageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"recreio-musical-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task Home_page_renders_the_playlist_shell()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(
                "ConnectionStrings:Playlist",
                $"Data Source={_databasePath};Pooling=False"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recreio Musical", html, StringComparison.Ordinal);
        Assert.Contains("Playlist do recreio", html, StringComparison.Ordinal);
        Assert.Contains("logo-objetivo.png", html, StringComparison.Ordinal);
        Assert.Contains("Colégio Objetivo", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Fase 4", html, StringComparison.Ordinal);
        Assert.DoesNotContain("rm-dev-note", html, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(html, "Vaga disponível"));
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

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
