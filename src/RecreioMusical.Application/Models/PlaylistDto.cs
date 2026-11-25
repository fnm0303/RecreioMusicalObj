using RecreioMusical.Domain;

namespace RecreioMusical.Application.Models;

/// <summary>Estado completo da playlist compartilhada.</summary>
public sealed record PlaylistDto(IReadOnlyList<PlaylistItemDto> Items)
{
    public int Capacity => PlaylistRules.Capacity;

    public int Count => Items.Count;

    public bool IsFull => Count >= Capacity;

    public static PlaylistDto From(IEnumerable<PlaylistItem> items) =>
        new(items
            .OrderBy(item => item.Slot)
            .Select(PlaylistItemDto.FromDomain)
            .ToArray());
}
