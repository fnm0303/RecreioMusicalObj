namespace RecreioMusical.Application.Common;

/// <summary>Códigos de erro dos casos de uso e da integração com serviços externos.</summary>
public static class ApplicationErrors
{
    public const string InvalidTrackId = "invalid_track_id";
    public const string InvalidSearchQuery = "invalid_search_query";
    public const string PlaylistFull = "playlist_full";
    public const string PlaylistBusy = "playlist_busy";
    public const string DuplicateTrack = "duplicate_track";
    public const string TrackNotFound = "track_not_found";
    public const string SpotifyUnavailable = "spotify_unavailable";
    public const string ItemNotFound = "item_not_found";
}
