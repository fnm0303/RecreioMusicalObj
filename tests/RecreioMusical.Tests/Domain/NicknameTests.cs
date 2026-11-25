using RecreioMusical.Domain;

namespace RecreioMusical.Tests.Domain;

public sealed class NicknameTests
{
    [Theory]
    [InlineData("  Ana   Maria  ", "Ana Maria")]
    [InlineData("João", "João")]
    [InlineData("  Bia\tLima  ", "Bia Lima")]
    [InlineData("Zé", "Zé")]
    public void Create_normalizes_whitespace(string rawValue, string expected)
    {
        var nickname = Nickname.Create(rawValue);

        Assert.Equal(expected, nickname.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_value(string? rawValue)
    {
        var exception = Assert.Throws<DomainException>(() => Nickname.Create(rawValue));

        Assert.Equal(PlaylistErrors.InvalidNickname, exception.Code);
    }

    [Fact]
    public void Create_rejects_one_character()
    {
        var exception = Assert.Throws<DomainException>(() => Nickname.Create("A"));

        Assert.Equal(PlaylistErrors.InvalidNickname, exception.Code);
    }

    [Fact]
    public void Create_rejects_more_than_thirty_characters()
    {
        var exception = Assert.Throws<DomainException>(() => Nickname.Create(new string('a', 31)));

        Assert.Equal(PlaylistErrors.InvalidNickname, exception.Code);
    }

    [Fact]
    public void Create_accepts_boundary_lengths()
    {
        Assert.Equal(2, Nickname.Create("AB").Value.Length);
        Assert.Equal(30, Nickname.Create(new string('a', 30)).Value.Length);
    }

    [Fact]
    public void ToString_returns_normalized_value()
    {
        var nickname = Nickname.Create("  Ana  ");

        Assert.Equal("Ana", nickname.ToString());
    }
}
