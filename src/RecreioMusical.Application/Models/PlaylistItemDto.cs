using RecreioMusical.Domain;

namespace RecreioMusical.Application.Models;

/// <summary>Uma vaga preenchida da playlist.</summary>
public sealed record PlaylistItemDto(
    int Slot,
    string TrackId,
    string Title,
    string Artists,
    string AlbumName,
    string? CoverUrl,
    int DurationMs,
    string SpotifyUrl,
    string AddedBy,
    DateTimeOffset AddedAt)
{
    public static PlaylistItemDto FromDomain(PlaylistItem item) =>
        new(
            item.Slot,
            item.Track.Id,
            item.Track.Title,
            item.Track.Artists,
            item.Track.AlbumName,
            item.Track.CoverUrl,
            item.Track.DurationMs,
            item.Track.SpotifyUrl,
            item.AddedBy.Value,
            item.AddedAt);
}
