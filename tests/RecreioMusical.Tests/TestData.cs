using RecreioMusical.Application.Models;
using RecreioMusical.Domain;

namespace RecreioMusical.Tests;

internal static class TestData
{
    public const string DefaultTrackId = "4uLU6hMCjMI75M1A2tKUQC";

    public static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 30, 0, TimeSpan.FromHours(-3));

    public static TrackDto Track(
        string? id = null,
        string title = "Never Gonna Give You Up",
        string artists = "Rick Astley",
        string album = "Whenever You Need Somebody",
        string? coverUrl = "https://i.scdn.co/image/ab67616d0000b273",
        int durationMs = 213_573)
    {
        var trackId = id ?? DefaultTrackId;

        return new TrackDto(
            trackId,
            title,
            artists,
            album,
            coverUrl,
            durationMs,
            $"https://open.spotify.com/track/{trackId}");
    }

    /// <summary>Gera um ID de 22 caracteres alfanuméricos para testes.</summary>
    public static string TrackId(int seed) => seed.ToString("D22");

    public static Nickname CreateNickname(string value = "Ana") => Nickname.Create(value);

    public static PlaylistItem Item(
        int slot,
        string? trackId = null,
        string addedBy = "Ana",
        DateTimeOffset? addedAt = null)
    {
        var id = trackId ?? TrackId(slot);

        return PlaylistItem.Place(
            slot,
            TrackInfo.Create(
                id,
                $"Música {slot}",
                "Artista",
                "Álbum",
                null,
                180_000,
                $"https://open.spotify.com/track/{id}"),
            Nickname.Create(addedBy),
            addedAt ?? Now);
    }
}
