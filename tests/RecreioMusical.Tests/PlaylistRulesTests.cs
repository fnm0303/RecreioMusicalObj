using RecreioMusical.Domain;

namespace RecreioMusical.Tests;

public sealed class PlaylistRulesTests
{
    [Fact]
    public void Playlist_holds_exactly_five_songs()
    {
        Assert.Equal(5, PlaylistRules.Capacity);
        Assert.Equal(1, PlaylistRules.FirstSlot);
        Assert.Equal(PlaylistRules.Capacity, PlaylistRules.LastSlot);
    }
}
