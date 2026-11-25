using RecreioMusical.Application.UseCases;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Application;

public sealed class ClearPlaylistHandlerTests
{
    [Fact]
    public async Task Handle_clears_playlist_and_publishes_empty_state()
    {
        var repository = new FakePlaylistRepository();
        repository.Seed(TestData.Item(1), TestData.Item(2), TestData.Item(3));
        var notifier = new RecordingPlaylistNotifier();
        var handler = new ClearPlaylistHandler(repository, notifier);

        var result = await handler.HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Items);

        var published = Assert.Single(notifier.Published);
        Assert.Empty(published.Items);
        Assert.Equal(0, published.Count);
    }

    [Fact]
    public async Task Handle_publishes_even_when_playlist_is_already_empty()
    {
        var repository = new FakePlaylistRepository();
        var notifier = new RecordingPlaylistNotifier();
        var handler = new ClearPlaylistHandler(repository, notifier);

        await handler.HandleAsync();

        Assert.Single(notifier.Published);
    }
}
