namespace RecreioMusical.Infrastructure.Spotify;

/// <summary>
/// Credenciais e parâmetros da API do Spotify.
/// </summary>
public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    /// <summary>Client ID do app criado no Spotify Developer Dashboard.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret do app. Nunca deve chegar ao navegador.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Mercado usado na busca de faixas.</summary>
    public string Market { get; set; } = "BR";

    /// <summary>Quantidade máxima de faixas retornadas por busca.</summary>
    public int SearchLimit { get; set; } = 10;

    /// <summary>
    /// Indica se o app já pode autenticar na API do Spotify.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}
