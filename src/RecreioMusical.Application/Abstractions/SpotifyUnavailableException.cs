namespace RecreioMusical.Application.Abstractions;

/// <summary>Falha temporária de rede, autenticação ou limite de uso do Spotify.</summary>
public sealed class SpotifyUnavailableException : Exception
{
    public SpotifyUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
