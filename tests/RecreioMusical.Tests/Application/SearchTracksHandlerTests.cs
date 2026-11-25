using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Application;

public sealed class SearchTracksHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" a ")]
    public async Task Handle_with_invalid_query_returns_failure(string? query)
    {
        var spotify = new FakeSpotifyCatalogService();
        var handler = new SearchTracksHandler(spotify);

        var result = await handler.HandleAsync(new SearchTracksQuery(query));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.InvalidSearchQuery, result.Error!.Code);
        Assert.Empty(spotify.SearchQueries);
    }

    [Fact]
    public async Task Handle_trims_query_before_searching()
    {
        var spotify = new FakeSpotifyCatalogService();
        var handler = new SearchTracksHandler(spotify);

        await handler.HandleAsync(new SearchTracksQuery("  daft punk  "));

        Assert.Equal(new[] { "daft punk" }, spotify.SearchQueries);
    }

    [Fact]
    public async Task Handle_returns_catalog_results()
    {
        var expected = TestData.Track();
        var spotify = new FakeSpotifyCatalogService { SearchResults = [expected] };
        var handler = new SearchTracksHandler(spotify);

        var result = await handler.HandleAsync(new SearchTracksQuery("rick astley"));

        Assert.True(result.IsSuccess);
        Assert.Same(expected, Assert.Single(result.Value));
    }

    [Fact]
    public async Task Handle_rejects_query_over_the_limit()
    {
        var spotify = new FakeSpotifyCatalogService();
        var handler = new SearchTracksHandler(spotify);
        var query = new string('a', SearchTracksHandler.MaxQueryLength + 1);

        var result = await handler.HandleAsync(new SearchTracksQuery(query));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.InvalidSearchQuery, result.Error!.Code);
        Assert.Empty(spotify.SearchQueries);
    }

    [Fact]
    public async Task Handle_maps_spotify_failure()
    {
        var spotify = new FakeSpotifyCatalogService
        {
            SearchFailure = new SpotifyUnavailableException("offline")
        };
        var handler = new SearchTracksHandler(spotify);

        var result = await handler.HandleAsync(new SearchTracksQuery("daft punk"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.SpotifyUnavailable, result.Error!.Code);
    }
}
