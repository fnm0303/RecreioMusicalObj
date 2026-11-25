using Microsoft.AspNetCore.Mvc.Testing;

namespace RecreioMusical.Tests;

public sealed class HomePageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomePageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_page_renders_the_playlist_shell()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recreio Musical", html, StringComparison.Ordinal);
        Assert.Contains("Playlist do recreio", html, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(html, "Vaga disponível"));
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
