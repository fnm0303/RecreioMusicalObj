namespace RecreioMusical.Domain;

/// <summary>
/// Dados essenciais de uma faixa, já revalidados a partir do catálogo do Spotify.
/// </summary>
public sealed class TrackInfo
{
    public const int MaxIdLength = 64;
    public const int MaxTitleLength = 200;
    public const int MaxArtistsLength = 300;
    public const int MaxAlbumLength = 200;
    public const int MaxUrlLength = 500;

    private TrackInfo(
        string id,
        string title,
        string artists,
        string albumName,
        string? coverUrl,
        int durationMs,
        string spotifyUrl)
    {
        Id = id;
        Title = title;
        Artists = artists;
        AlbumName = albumName;
        CoverUrl = coverUrl;
        DurationMs = durationMs;
        SpotifyUrl = spotifyUrl;
    }

    public string Id { get; }

    public string Title { get; }

    public string Artists { get; }

    public string AlbumName { get; }

    public string? CoverUrl { get; }

    public int DurationMs { get; }

    public string SpotifyUrl { get; }

    public static TrackInfo Create(
        string? id,
        string? title,
        string? artists,
        string? albumName,
        string? coverUrl,
        int durationMs,
        string? spotifyUrl)
    {
        if (durationMs < 0)
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, "A duração da música não pode ser negativa.");
        }

        return new TrackInfo(
            Required(id, "O identificador", MaxIdLength),
            Required(title, "O título", MaxTitleLength),
            Required(artists, "Os artistas", MaxArtistsLength),
            Optional(albumName, "O álbum", MaxAlbumLength),
            OptionalUrl(coverUrl, "A capa"),
            durationMs,
            RequiredUrl(spotifyUrl, "O link do Spotify"));
    }

    private static string Required(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, $"{fieldName} da música é obrigatório.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, $"{fieldName} da música excede {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string Optional(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length > maxLength)
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, $"{fieldName} da música excede {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string RequiredUrl(string? value, string fieldName)
    {
        var normalized = Required(value, fieldName, MaxUrlLength);

        if (!IsAbsoluteHttpUrl(normalized))
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, $"{fieldName} da música é inválido.");
        }

        return normalized;
    }

    private static string? OptionalUrl(string? value, string fieldName)
    {
        var normalized = Optional(value, fieldName, MaxUrlLength);

        if (normalized.Length == 0)
        {
            return null;
        }

        if (!IsAbsoluteHttpUrl(normalized))
        {
            throw new DomainException(PlaylistErrors.InvalidTrack, $"{fieldName} da música é inválida.");
        }

        return normalized;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
