using RecreioMusical.Domain;

namespace RecreioMusical.Tests.Domain;

public sealed class PlaylistRulesTests
{
    [Fact]
    public void Playlist_holds_exactly_five_songs()
    {
        Assert.Equal(5, PlaylistRules.Capacity);
        Assert.Equal(1, PlaylistRules.FirstSlot);
        Assert.Equal(PlaylistRules.Capacity, PlaylistRules.LastSlot);
    }

    [Fact]
    public void NextFreeSlot_returns_first_slot_when_empty()
    {
        Assert.Equal(1, PlaylistRules.NextFreeSlot([]));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 4, 5 }, 3)]
    [InlineData(new[] { 2, 3 }, 1)]
    [InlineData(new[] { 1, 3, 4 }, 2)]
    [InlineData(new[] { 1, 2, 3, 4 }, 5)]
    public void NextFreeSlot_returns_lowest_gap(int[] occupied, int expected)
    {
        Assert.Equal(expected, PlaylistRules.NextFreeSlot(occupied));
    }

    [Fact]
    public void NextFreeSlot_returns_null_when_full()
    {
        Assert.Null(PlaylistRules.NextFreeSlot([1, 2, 3, 4, 5]));
    }

    [Fact]
    public void NextFreeSlot_ignores_values_outside_the_playlist()
    {
        Assert.Equal(1, PlaylistRules.NextFreeSlot([0, 6, 42]));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void IsValidSlot_checks_the_range(int slot, bool expected)
    {
        Assert.Equal(expected, PlaylistRules.IsValidSlot(slot));
    }
}
