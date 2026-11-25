using RecreioMusical.Application.Abstractions;
using RecreioMusical.Domain;

namespace RecreioMusical.Tests.Fakes;

internal sealed class FakePlaylistRepository : IPlaylistRepository
{
    private readonly List<PlaylistItem> _items = [];

    public IReadOnlyList<PlaylistItem> Items => _items;

    public int AddAttempts { get; private set; }

    public int RemoveCalls { get; private set; }

    /// <summary>Na primeira inserção, simula outra pessoa ocupando a mesma vaga.</summary>
    public bool SimulateSlotRaceOnce { get; set; }

    public void Seed(params PlaylistItem[] items) => _items.AddRange(items);

    public Task<IReadOnlyList<PlaylistItem>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlaylistItem>>(
            _items.OrderBy(item => item.Slot).ToArray());

    public Task<IReadOnlyList<int>> GetOccupiedSlotsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<int>>(
            _items.Select(item => item.Slot).ToArray());

    public Task<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Any(item => item.Track.Id == trackId));

    public Task<AddPlaylistItemOutcome> TryAddAsync(
        PlaylistItem item,
        CancellationToken cancellationToken)
    {
        AddAttempts++;

        if (SimulateSlotRaceOnce)
        {
            SimulateSlotRaceOnce = false;
            _items.Add(TestData.Item(item.Slot, TestData.TrackId(99), "Bia", item.AddedAt));
            return Task.FromResult(AddPlaylistItemOutcome.SlotTaken);
        }

        if (_items.Any(existing => existing.Track.Id == item.Track.Id))
        {
            return Task.FromResult(AddPlaylistItemOutcome.DuplicateTrack);
        }

        if (_items.Any(existing => existing.Slot == item.Slot))
        {
            return Task.FromResult(AddPlaylistItemOutcome.SlotTaken);
        }

        _items.Add(item);
        return Task.FromResult(AddPlaylistItemOutcome.Added);
    }

    public Task<bool> RemoveAsync(int slot, CancellationToken cancellationToken)
    {
        RemoveCalls++;
        return Task.FromResult(_items.RemoveAll(item => item.Slot == slot) > 0);
    }

    public Task<int> ClearAsync(CancellationToken cancellationToken)
    {
        var removed = _items.Count;
        _items.Clear();
        return Task.FromResult(removed);
    }
}
