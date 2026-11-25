using RecreioMusical.Domain;

namespace RecreioMusical.Tests.Domain;

public sealed class TrackInfoTests
{
    private const string SpotifyUrl = "https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC";

    [Fact]
    public void Create_trims_text_fields()
    {
        var track = Create(
            id: " 4uLU6hMCjMI75M1A2tKUQC ",
            title: "  Song  ",
            artists: "  Artist  ",
            album: "  Album  ",
            cover: " https://i.scdn.co/image/abc ",
            url: $" {SpotifyUrl} ");

        Assert.Equal("4uLU6hMCjMI75M1A2tKUQC", track.Id);
        Assert.Equal("Song", track.Title);
        Assert.Equal("Artist", track.Artists);
        Assert.Equal("Album", track.AlbumName);
        Assert.Equal("https://i.scdn.co/image/abc", track.CoverUrl);
        Assert.Equal(SpotifyUrl, track.SpotifyUrl);
    }

    [Fact]
    public void Create_accepts_missing_cover()
    {
        var track = Create(cover: null);

        Assert.Null(track.CoverUrl);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public void Create_rejects_blank_title(string title)
    {
        var exception = Assert.Throws<DomainException>(() => Create(title: title));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    [Fact]
    public void Create_rejects_blank_artists()
    {
        var exception = Assert.Throws<DomainException>(() => Create(artists: " "));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/track/123")]
    [InlineData("ftp://open.spotify.com/track/123")]
    public void Create_rejects_invalid_spotify_url(string url)
    {
        var exception = Assert.Throws<DomainException>(() => Create(url: url));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    [Fact]
    public void Create_rejects_invalid_cover_url()
    {
        var exception = Assert.Throws<DomainException>(() => Create(cover: "capa.jpg"));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    [Fact]
    public void Create_rejects_negative_duration()
    {
        var exception = Assert.Throws<DomainException>(() => Create(durationMs: -1));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    [Fact]
    public void Create_rejects_title_over_limit()
    {
        var exception = Assert.Throws<DomainException>(
            () => Create(title: new string('a', TrackInfo.MaxTitleLength + 1)));

        Assert.Equal(PlaylistErrors.InvalidTrack, exception.Code);
    }

    private static TrackInfo Create(
        string id = "4uLU6hMCjMI75M1A2tKUQC",
        string title = "Song",
        string artists = "Artist",
        string album = "Album",
        string? cover = "https://i.scdn.co/image/abc",
        int durationMs = 180_000,
        string url = SpotifyUrl) =>
        TrackInfo.Create(id, title, artists, album, cover, durationMs, url);
}
