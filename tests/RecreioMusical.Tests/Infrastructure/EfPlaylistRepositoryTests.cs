using RecreioMusical.Application.Abstractions;
using RecreioMusical.Domain;
using RecreioMusical.Infrastructure.Persistence;

namespace RecreioMusical.Tests.Infrastructure;

public sealed class EfPlaylistRepositoryTests
{
    [Fact]
    public async Task GetAll_returns_empty_playlist()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        var items = await repository.GetAllAsync(CancellationToken.None);

        Assert.Empty(items);
        Assert.Empty(await repository.GetOccupiedSlotsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryAdd_persists_the_item_with_all_fields()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);
        var item = TestData.Item(3, TestData.DefaultTrackId, "  Bia  ");

        var outcome = await repository.TryAddAsync(item, CancellationToken.None);

        Assert.Equal(AddPlaylistItemOutcome.Added, outcome);

        var stored = Assert.Single(await repository.GetAllAsync(CancellationToken.None));
        Assert.Equal(3, stored.Slot);
        Assert.Equal(TestData.DefaultTrackId, stored.Track.Id);
        Assert.Equal(item.Track.Title, stored.Track.Title);
        Assert.Equal("Bia", stored.AddedBy.Value);
        Assert.Equal(item.AddedAt, stored.AddedAt);
    }

    [Fact]
    public async Task GetAll_orders_by_slot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(TestData.Item(5), CancellationToken.None);
        await repository.TryAddAsync(TestData.Item(2), CancellationToken.None);
        await repository.TryAddAsync(TestData.Item(4), CancellationToken.None);

        var items = await repository.GetAllAsync(CancellationToken.None);

        Assert.Equal(new[] { 2, 4, 5 }, items.Select(item => item.Slot));
        Assert.Equal(
            new[] { 2, 4, 5 },
            await repository.GetOccupiedSlotsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryAdd_returns_duplicate_track_for_the_same_song()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(
            TestData.Item(1, TestData.DefaultTrackId),
            CancellationToken.None);

        var outcome = await repository.TryAddAsync(
            TestData.Item(2, TestData.DefaultTrackId),
            CancellationToken.None);

        Assert.Equal(AddPlaylistItemOutcome.DuplicateTrack, outcome);
        Assert.Single(await repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryAdd_returns_slot_taken_when_the_slot_is_occupied()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(TestData.Item(1, TestData.TrackId(1)), CancellationToken.None);

        var outcome = await repository.TryAddAsync(
            TestData.Item(1, TestData.TrackId(2)),
            CancellationToken.None);

        Assert.Equal(AddPlaylistItemOutcome.SlotTaken, outcome);
        Assert.Single(await repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryAdd_can_reuse_a_freed_slot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(TestData.Item(1, TestData.TrackId(1)), CancellationToken.None);
        await repository.RemoveAsync(1, CancellationToken.None);

        var outcome = await repository.TryAddAsync(
            TestData.Item(1, TestData.TrackId(2)),
            CancellationToken.None);

        Assert.Equal(AddPlaylistItemOutcome.Added, outcome);
        var stored = Assert.Single(await repository.GetAllAsync(CancellationToken.None));
        Assert.Equal(TestData.TrackId(2), stored.Track.Id);
    }

    [Fact]
    public async Task Data_survives_a_new_context()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var firstContext = database.CreateContext())
        {
            var firstRepository = new EfPlaylistRepository(firstContext);
            await firstRepository.TryAddAsync(TestData.Item(2), CancellationToken.None);
        }

        await using var secondContext = database.CreateContext();
        var secondRepository = new EfPlaylistRepository(secondContext);

        var stored = Assert.Single(await secondRepository.GetAllAsync(CancellationToken.None));
        Assert.Equal(2, stored.Slot);
    }

    [Fact]
    public async Task ContainsTrack_checks_only_existing_tracks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(
            TestData.Item(1, TestData.DefaultTrackId),
            CancellationToken.None);

        Assert.True(await repository.ContainsTrackAsync(TestData.DefaultTrackId, CancellationToken.None));
        Assert.False(await repository.ContainsTrackAsync(TestData.TrackId(9), CancellationToken.None));
    }

    [Fact]
    public async Task Remove_returns_false_for_an_empty_slot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        Assert.False(await repository.RemoveAsync(3, CancellationToken.None));
    }

    [Fact]
    public async Task Remove_deletes_only_the_requested_slot()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        await repository.TryAddAsync(TestData.Item(1), CancellationToken.None);
        await repository.TryAddAsync(TestData.Item(2), CancellationToken.None);

        Assert.True(await repository.RemoveAsync(1, CancellationToken.None));

        var remaining = Assert.Single(await repository.GetAllAsync(CancellationToken.None));
        Assert.Equal(2, remaining.Slot);
    }

    [Fact]
    public async Task Clear_removes_every_item_and_returns_the_count()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new EfPlaylistRepository(context);

        for (var slot = PlaylistRules.FirstSlot; slot <= PlaylistRules.LastSlot; slot++)
        {
            await repository.TryAddAsync(TestData.Item(slot), CancellationToken.None);
        }

        var removed = await repository.ClearAsync(CancellationToken.None);

        Assert.Equal(PlaylistRules.Capacity, removed);
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
    }
}
