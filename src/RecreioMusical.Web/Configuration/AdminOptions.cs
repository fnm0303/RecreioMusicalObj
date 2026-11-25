namespace RecreioMusical.Web.Configuration;

/// <summary>
/// Configuração das ações administrativas da playlist.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>PIN usado para remover músicas e limpar a playlist.</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>
    /// A administração só existe quando um PIN foi configurado fora do repositório.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Pin);
}
