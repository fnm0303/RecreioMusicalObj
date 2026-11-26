using Microsoft.AspNetCore.Mvc;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Web.Api;

namespace RecreioMusical.Web.Controllers;

[ApiController]
[Route("api/tracks")]
public sealed class TracksController : ControllerBase
{
    private readonly SearchTracksHandler _searchTracks;

    public TracksController(SearchTracksHandler searchTracks)
    {
        ArgumentNullException.ThrowIfNull(searchTracks);
        _searchTracks = searchTracks;
    }

    /// <summary>Busca faixas no catálogo do Spotify.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var result = await _searchTracks.HandleAsync(
            new SearchTracksQuery(q),
            cancellationToken);

        return result.IsFailure
            ? ApiProblems.FromResult(this, result)
            : Ok(new SearchTracksResponse(result.Value));
    }
}
