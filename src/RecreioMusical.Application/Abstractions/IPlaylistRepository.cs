using RecreioMusical.Domain;

namespace RecreioMusical.Application.Abstractions;

/// <summary>
/// Persistência da playlist. A implementação deve garantir, no banco, que não existam
/// duas músicas na mesma vaga nem a mesma faixa repetida.
/// </summary>
public interface IPlaylistRepository
{
    Task<IReadOnlyList<PlaylistItem>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetOccupiedSlotsAsync(CancellationToken cancellationToken);

    Task<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken);

    Task<AddPlaylistItemOutcome> TryAddAsync(
        PlaylistItem item,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(int slot, CancellationToken cancellationToken);

    Task<int> ClearAsync(CancellationToken cancellationToken);
}

/// <summary>Resultado de uma tentativa de inserção, incluindo conflitos de concorrência.</summary>
public enum AddPlaylistItemOutcome
{
    Added,
    SlotTaken,
    DuplicateTrack
}
