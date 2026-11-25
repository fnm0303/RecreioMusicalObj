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

    /// <summary>Indica se o número informado corresponde a uma vaga válida.</summary>
    public static bool IsValidSlot(int slot) => slot is >= FirstSlot and <= LastSlot;

    /// <summary>
    /// Retorna a primeira vaga livre, preenchendo os buracos antes de avançar.
    /// Retorna <c>null</c> quando todas as vagas estão ocupadas.
    /// </summary>
    public static int? NextFreeSlot(IEnumerable<int> occupiedSlots)
    {
        ArgumentNullException.ThrowIfNull(occupiedSlots);

        var occupied = occupiedSlots as ISet<int> ?? new HashSet<int>(occupiedSlots);

        for (var slot = FirstSlot; slot <= LastSlot; slot++)
        {
            if (!occupied.Contains(slot))
            {
                return slot;
            }
        }

        return null;
    }
}
