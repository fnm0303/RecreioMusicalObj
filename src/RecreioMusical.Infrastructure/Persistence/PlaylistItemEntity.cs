namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Representação da linha da playlist no banco. É intencionalmente separada de
/// <see cref="RecreioMusical.Domain.PlaylistItem"/> para que o domínio permaneça sem dependências
/// de persistência.
/// </summary>
internal sealed class PlaylistItemEntity
{
    public int Slot { get; set; }

    public string TrackId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Artists { get; set; } = string.Empty;

    public string AlbumName { get; set; } = string.Empty;

    public string? CoverUrl { get; set; }

    public int DurationMs { get; set; }

    public string SpotifyUrl { get; set; } = string.Empty;

    public string AddedBy { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }
}
