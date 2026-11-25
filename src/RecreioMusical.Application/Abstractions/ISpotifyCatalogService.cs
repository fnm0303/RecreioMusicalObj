using RecreioMusical.Application.Models;

namespace RecreioMusical.Application.Abstractions;

/// <summary>
/// Acesso ao catálogo do Spotify. A implementação roda no servidor; o segredo nunca
/// chega ao navegador.
/// </summary>
public interface ISpotifyCatalogService
{
    Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken);

    Task<TrackDto?> GetTrackAsync(
        string trackId,
        CancellationToken cancellationToken);
}
