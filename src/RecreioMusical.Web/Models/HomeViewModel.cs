using RecreioMusical.Domain;

namespace RecreioMusical.Web.Models;

public sealed class HomeViewModel
{
    public int PlaylistCapacity { get; init; } = PlaylistRules.Capacity;

    public bool ShowDevelopmentNote { get; init; }

    public bool ShowSpotifySetupHint { get; init; }
}
