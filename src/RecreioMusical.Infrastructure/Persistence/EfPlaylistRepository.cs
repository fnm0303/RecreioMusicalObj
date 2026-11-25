using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Domain;

namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Repositório da playlist sobre SQLite. As violações de índice único são traduzidas
/// em resultados de conflito, permitindo que o caso de uso tente outra vaga em vez de
/// devolver um erro inesperado ao usuário.
/// </summary>
public sealed class EfPlaylistRepository : IPlaylistRepository
{
    private const string DuplicateTrackColumn = "PlaylistItems.TrackId";
    private const int SqliteConstraintErrorCode = 19;

    private readonly PlaylistDbContext _db;

    public EfPlaylistRepository(PlaylistDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<IReadOnlyList<PlaylistItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.PlaylistItems
            .AsNoTracking()
            .OrderBy(item => item.Slot)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<int>> GetOccupiedSlotsAsync(CancellationToken cancellationToken)
    {
        var slots = await _db.PlaylistItems
            .AsNoTracking()
            .OrderBy(item => item.Slot)
            .Select(item => item.Slot)
            .ToListAsync(cancellationToken);

        return slots;
    }

    public Task<bool> ContainsTrackAsync(string trackId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        return _db.PlaylistItems
            .AsNoTracking()
            .AnyAsync(item => item.TrackId == trackId, cancellationToken);
    }

    public async Task<AddPlaylistItemOutcome> TryAddAsync(
        PlaylistItem item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        var entity = ToEntity(item);
        _db.PlaylistItems.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            _db.Entry(entity).State = EntityState.Detached;
            return AddPlaylistItemOutcome.Added;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            _db.Entry(entity).State = EntityState.Detached;

            return IsDuplicateTrackViolation(exception)
                ? AddPlaylistItemOutcome.DuplicateTrack
                : AddPlaylistItemOutcome.SlotTaken;
        }
    }

    public async Task<bool> RemoveAsync(int slot, CancellationToken cancellationToken)
    {
        var removed = await _db.PlaylistItems
            .Where(item => item.Slot == slot)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    public Task<int> ClearAsync(CancellationToken cancellationToken) =>
        _db.PlaylistItems.ExecuteDeleteAsync(cancellationToken);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException inner &&
        inner.SqliteErrorCode == SqliteConstraintErrorCode &&
        inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsDuplicateTrackViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            DuplicateTrackColumn,
            StringComparison.OrdinalIgnoreCase) == true;

    private static PlaylistItemEntity ToEntity(PlaylistItem item) => new()
    {
        Slot = item.Slot,
        TrackId = item.Track.Id,
        Title = item.Track.Title,
        Artists = item.Track.Artists,
        AlbumName = item.Track.AlbumName,
        CoverUrl = item.Track.CoverUrl,
        DurationMs = item.Track.DurationMs,
        SpotifyUrl = item.Track.SpotifyUrl,
        AddedBy = item.AddedBy.Value,
        AddedAt = item.AddedAt
    };

    private static PlaylistItem ToDomain(PlaylistItemEntity entity) =>
        PlaylistItem.Place(
            entity.Slot,
            TrackInfo.Create(
                entity.TrackId,
                entity.Title,
                entity.Artists,
                entity.AlbumName,
                entity.CoverUrl,
                entity.DurationMs,
                entity.SpotifyUrl),
            Nickname.Create(entity.AddedBy),
            entity.AddedAt);
}
