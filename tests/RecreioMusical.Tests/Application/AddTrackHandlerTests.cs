using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Domain;
using RecreioMusical.Tests.Fakes;

namespace RecreioMusical.Tests.Application;

public sealed class AddTrackHandlerTests
{
    [Fact]
    public async Task Handle_adds_to_first_free_slot_and_publishes()
    {
        var (handler, repository, _, notifier) = CreateSut();

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "  Ana  "));

        Assert.True(result.IsSuccess);
        var item = result.Value;
        Assert.Equal(1, item.Slot);
        Assert.Equal(TestData.DefaultTrackId, item.TrackId);
        Assert.Equal("Ana", item.AddedBy);
        Assert.Equal(TestData.Now, item.AddedAt);
        Assert.Single(repository.Items);

        var published = Assert.Single(notifier.Published);
        Assert.Equal(1, published.Count);
        Assert.Equal(5, published.Capacity);
        Assert.False(published.IsFull);
    }

    [Fact]
    public async Task Handle_reconfirms_track_data_with_the_catalog()
    {
        var (handler, _, spotify, _) = CreateSut();
        spotify.TrackResult = TestData.Track(title: "Título confirmado no servidor");

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.Equal("Título confirmado no servidor", result.Value.Title);
        Assert.Equal(new[] { TestData.DefaultTrackId }, spotify.RequestedTrackIds);
    }

    [Fact]
    public async Task Handle_fills_the_lowest_gap()
    {
        var (handler, repository, _, _) = CreateSut();
        repository.Seed(
            TestData.Item(1),
            TestData.Item(2),
            TestData.Item(4),
            TestData.Item(5));

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Slot);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            repository.Items.Select(item => item.Slot).OrderBy(slot => slot));
    }

    [Fact]
    public async Task Handle_rejects_when_playlist_is_full_without_calling_spotify()
    {
        var (handler, repository, spotify, _) = CreateSut();
        for (var slot = 1; slot <= PlaylistRules.Capacity; slot++)
        {
            repository.Seed(TestData.Item(slot));
        }

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.PlaylistFull, result.Error!.Code);
        Assert.Empty(spotify.RequestedTrackIds);
        Assert.Equal(0, repository.AddAttempts);
    }

    [Fact]
    public async Task Handle_rejects_duplicate_track_without_calling_spotify()
    {
        var (handler, repository, spotify, _) = CreateSut();
        repository.Seed(TestData.Item(1, TestData.DefaultTrackId));

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.DuplicateTrack, result.Error!.Code);
        Assert.Empty(spotify.RequestedTrackIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public async Task Handle_rejects_invalid_nickname(string addedBy)
    {
        var (handler, _, spotify, _) = CreateSut();

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, addedBy));

        Assert.True(result.IsFailure);
        Assert.Equal(PlaylistErrors.InvalidNickname, result.Error!.Code);
        Assert.Empty(spotify.RequestedTrackIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4uLU6hMCjMI75M1A2tKUQ!")]
    public async Task Handle_rejects_invalid_track_id(string trackId)
    {
        var (handler, _, spotify, _) = CreateSut();

        var result = await handler.HandleAsync(new AddTrackCommand(trackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.InvalidTrackId, result.Error!.Code);
        Assert.Empty(spotify.RequestedTrackIds);
    }

    [Fact]
    public async Task Handle_rejects_track_not_found()
    {
        var (handler, _, spotify, _) = CreateSut();
        spotify.TrackResult = null;

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.TrackNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_maps_spotify_failure()
    {
        var (handler, _, spotify, _) = CreateSut();
        spotify.TrackFailure = new SpotifyUnavailableException("offline");

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.SpotifyUnavailable, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_rejects_malformed_track_from_catalog()
    {
        var (handler, _, spotify, _) = CreateSut();
        spotify.TrackResult = TestData.Track(title: "   ");

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsFailure);
        Assert.Equal(PlaylistErrors.InvalidTrack, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_retries_after_slot_race()
    {
        var (handler, repository, _, notifier) = CreateSut();
        repository.SimulateSlotRaceOnce = true;

        var result = await handler.HandleAsync(
            new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Slot);
        Assert.Equal(2, repository.AddAttempts);
        Assert.Equal(
            new[] { 1, 2 },
            repository.Items.Select(item => item.Slot).OrderBy(slot => slot));

        var published = Assert.Single(notifier.Published);
        Assert.Equal(new[] { 1, 2 }, published.Items.Select(item => item.Slot));
    }

    [Fact]
    public async Task Handle_publishes_playlist_ordered_by_slot()
    {
        var (handler, repository, _, notifier) = CreateSut();
        repository.Seed(TestData.Item(2));

        await handler.HandleAsync(new AddTrackCommand(TestData.DefaultTrackId, "Ana"));

        var published = Assert.Single(notifier.Published);
        Assert.Equal(new[] { 1, 2 }, published.Items.Select(item => item.Slot));
        Assert.Equal(2, published.Count);
    }

    private static (
        AddTrackHandler Handler,
        FakePlaylistRepository Repository,
        FakeSpotifyCatalogService Spotify,
        RecordingPlaylistNotifier Notifier) CreateSut()
    {
        var repository = new FakePlaylistRepository();
        var spotify = new FakeSpotifyCatalogService
        {
            TrackResult = TestData.Track()
        };
        var notifier = new RecordingPlaylistNotifier();
        var handler = new AddTrackHandler(
            repository,
            spotify,
            notifier,
            new FixedTimeProvider(TestData.Now));

        return (handler, repository, spotify, notifier);
    }
}
