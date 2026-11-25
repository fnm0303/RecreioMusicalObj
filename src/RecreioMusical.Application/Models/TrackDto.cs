namespace RecreioMusical.Application.Models;

/// <summary>Faixa retornada pela busca do Spotify, pronta para a interface.</summary>
public sealed record TrackDto(
    string Id,
    string Title,
    string Artists,
    string AlbumName,
    string? CoverUrl,
    int DurationMs,
    string SpotifyUrl);
