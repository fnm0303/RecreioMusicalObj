using RecreioMusical.Application.UseCases;
using RecreioMusical.Domain;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Application;

public sealed class GetPlaylistHandlerTests
{
    [Fact]
    public async Task Handle_returns_items_ordered_by_slot()
    {
        var repository = new FakePlaylistRepository();
        repository.Seed(TestData.Item(3), TestData.Item(1));
        var handler = new GetPlaylistHandler(repository);

        var playlist = await handler.HandleAsync();

        Assert.Equal(new[] { 1, 3 }, playlist.Items.Select(item => item.Slot));
        Assert.Equal(2, playlist.Count);
        Assert.Equal(PlaylistRules.Capacity, playlist.Capacity);
        Assert.False(playlist.IsFull);
    }

    [Fact]
    public async Task Handle_returns_empty_playlist()
    {
        var handler = new GetPlaylistHandler(new FakePlaylistRepository());

        var playlist = await handler.HandleAsync();

        Assert.Empty(playlist.Items);
        Assert.Equal(0, playlist.Count);
        Assert.False(playlist.IsFull);
    }

    [Fact]
    public async Task Handle_marks_full_playlist()
    {
        var repository = new FakePlaylistRepository();
        for (var slot = 1; slot <= PlaylistRules.Capacity; slot++)
        {
            repository.Seed(TestData.Item(slot));
        }

        var handler = new GetPlaylistHandler(repository);

        var playlist = await handler.HandleAsync();

        Assert.True(playlist.IsFull);
        Assert.Equal(PlaylistRules.Capacity, playlist.Count);
    }
}
