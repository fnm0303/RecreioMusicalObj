using RecreioMusical.Application.Common;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Domain;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Application;

public sealed class RemoveTrackHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Handle_rejects_invalid_slot(int slot)
    {
        var (handler, repository, _) = CreateSut();

        var result = await handler.HandleAsync(new RemoveTrackCommand(slot));

        Assert.True(result.IsFailure);
        Assert.Equal(PlaylistErrors.InvalidSlot, result.Error!.Code);
        Assert.Equal(0, repository.RemoveCalls);
    }

    [Fact]
    public async Task Handle_returns_not_found_for_empty_slot()
    {
        var (handler, _, _) = CreateSut();

        var result = await handler.HandleAsync(new RemoveTrackCommand(3));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.ItemNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_removes_item_and_publishes_remaining_playlist()
    {
        var (handler, repository, notifier) = CreateSut();
        repository.Seed(TestData.Item(1), TestData.Item(2));

        var result = await handler.HandleAsync(new RemoveTrackCommand(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 2 }, repository.Items.Select(item => item.Slot));

        var published = Assert.Single(notifier.Published);
        Assert.Equal(new[] { 2 }, published.Items.Select(item => item.Slot));
        Assert.Equal(1, published.Count);
    }

    private static (
        RemoveTrackHandler Handler,
        FakePlaylistRepository Repository,
        RecordingPlaylistNotifier Notifier) CreateSut()
    {
        var repository = new FakePlaylistRepository();
        var notifier = new RecordingPlaylistNotifier();
        var handler = new RemoveTrackHandler(repository, notifier);

        return (handler, repository, notifier);
    }
}
