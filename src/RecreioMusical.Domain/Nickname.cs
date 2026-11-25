namespace RecreioMusical.Domain;

/// <summary>
/// Apelido de quem escolheu a música. Normaliza espaços e limita o tamanho,
/// sem exigir cadastro ou login.
/// </summary>
public sealed record Nickname
{
    public const int MinLength = 2;
    public const int MaxLength = 30;

    private Nickname(string value) => Value = value;

    public string Value { get; }

    public static Nickname Create(string? rawValue)
    {
        var normalized = Normalize(rawValue);

        if (normalized.Length == 0)
        {
            throw new DomainException(
                PlaylistErrors.InvalidNickname,
                "Informe um apelido para adicionar a música.");
        }

        if (normalized.Length < MinLength)
        {
            throw new DomainException(
                PlaylistErrors.InvalidNickname,
                $"O apelido deve ter pelo menos {MinLength} caracteres.");
        }

        if (normalized.Length > MaxLength)
        {
            throw new DomainException(
                PlaylistErrors.InvalidNickname,
                $"O apelido deve ter no máximo {MaxLength} caracteres.");
        }

        return new Nickname(normalized);
    }

    public override string ToString() => Value;

    private static string Normalize(string? rawValue) =>
        string.Join(' ', (rawValue ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
