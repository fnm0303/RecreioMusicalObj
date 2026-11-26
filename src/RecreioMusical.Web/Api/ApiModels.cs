using RecreioMusical.Application.Models;

namespace RecreioMusical.Web.Api;

public sealed record AddTrackRequest(string? TrackId, string? AddedBy);

public sealed record SearchTracksResponse(IReadOnlyList<TrackDto> Tracks);

public sealed record AddTrackResponse(PlaylistItemDto Item, PlaylistDto Playlist);
