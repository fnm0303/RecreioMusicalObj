namespace RecreioMusical.Domain;

/// <summary>
/// Violação de uma regra de negócio do domínio, identificada por um código estável.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
