using RecreioMusical.Domain;

namespace RecreioMusical.Tests.Domain;

public sealed class PlaylistItemTests
{
    [Fact]
    public void Place_stores_all_values()
    {
        var track = TrackInfo.Create(
            "4uLU6hMCjMI75M1A2tKUQC",
            "Song",
            "Artist",
            "Album",
            null,
            180_000,
            "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC");
        var nickname = Nickname.Create("Ana");
        var addedAt = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

        var item = PlaylistItem.Place(3, track, nickname, addedAt);

        Assert.Equal(3, item.Slot);
        Assert.Same(track, item.Track);
        Assert.Same(nickname, item.AddedBy);
        Assert.Equal(addedAt, item.AddedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Place_rejects_invalid_slot(int slot)
    {
        var track = TrackInfo.Create(
            "4uLU6hMCjMI75M1A2tKUQC",
            "Song",
            "Artist",
            "Album",
            null,
            180_000,
            "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC");

        var exception = Assert.Throws<DomainException>(
            () => PlaylistItem.Place(slot, track, Nickname.Create("Ana"), TestData.Now));

        Assert.Equal(PlaylistErrors.InvalidSlot, exception.Code);
    }

    [Fact]
    public void Place_rejects_null_track()
    {
        Assert.Throws<ArgumentNullException>(
            () => PlaylistItem.Place(1, null!, Nickname.Create("Ana"), TestData.Now));
    }

    [Fact]
    public void Place_rejects_null_nickname()
    {
        var track = TrackInfo.Create(
            "4uLU6hMCjMI75M1A2tKUQC",
            "Song",
            "Artist",
            "Album",
            null,
            180_000,
            "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC");

        Assert.Throws<ArgumentNullException>(
            () => PlaylistItem.Place(1, track, null!, TestData.Now));
    }
}
