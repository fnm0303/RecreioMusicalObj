namespace RecreioMusical.Application.Common;

/// <summary>
/// Identificadores de faixa do Spotify têm 22 caracteres alfanuméricos.
/// A validação evita chamadas desnecessárias à API.
/// </summary>
public static class SpotifyTrackId
{
    public const int Length = 22;

    public static bool IsValid(string? value) =>
        value is { Length: Length } && value.All(char.IsAsciiLetterOrDigit);
}
