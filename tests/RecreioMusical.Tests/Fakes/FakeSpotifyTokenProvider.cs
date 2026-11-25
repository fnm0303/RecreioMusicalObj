using RecreioMusical.Infrastructure.Spotify;

namespace RecreioMusical.Tests.Fakes;

internal sealed class FakeSpotifyTokenProvider : ISpotifyAccessTokenProvider
{
    public string Token { get; set; } = "token-1";

    public int Calls { get; private set; }

    public List<string> InvalidatedTokens { get; } = [];

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Token);
    }

    public void Invalidate(string accessToken)
    {
        InvalidatedTokens.Add(accessToken);

        if (string.Equals(accessToken, Token, StringComparison.Ordinal))
        {
            Token = "token-refreshed";
        }
    }
}
