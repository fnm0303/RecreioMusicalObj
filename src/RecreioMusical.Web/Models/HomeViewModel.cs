using RecreioMusical.Application.Models;
using RecreioMusical.Domain;

namespace RecreioMusical.Web.Models;

public sealed class HomeViewModel
{
    public int PlaylistCapacity { get; init; } = PlaylistRules.Capacity;

    public PlaylistDto Playlist { get; init; } = PlaylistDto.From([]);

    public bool ShowDevelopmentNote { get; init; }

    public bool ShowSpotifySetupHint { get; init; }

    public bool SearchEnabled { get; init; } = true;

    public bool AdminEnabled { get; init; }
}
