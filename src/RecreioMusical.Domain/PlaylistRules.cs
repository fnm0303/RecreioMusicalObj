namespace RecreioMusical.Domain;

/// <summary>
/// Regras imutáveis da playlist compartilhada do recreio.
/// </summary>
public static class PlaylistRules
{
    /// <summary>Número máximo de músicas na playlist.</summary>
    public const int Capacity = 5;

    /// <summary>Primeira vaga da playlist.</summary>
    public const int FirstSlot = 1;

    /// <summary>Última vaga da playlist.</summary>
    public const int LastSlot = Capacity;
}
