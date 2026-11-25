using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Application.UseCases;

public sealed record SearchTracksQuery(string? Query);

/// <summary>Valida o termo e consulta o catálogo do Spotify.</summary>
public sealed class SearchTracksHandler
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 80;

    private readonly ISpotifyCatalogService _catalog;

    public SearchTracksHandler(ISpotifyCatalogService catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public async Task<Result<IReadOnlyList<TrackDto>>> HandleAsync(
        SearchTracksQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var term = query.Query?.Trim() ?? string.Empty;

        if (term.Length < MinQueryLength)
        {
            return Result.Failure<IReadOnlyList<TrackDto>>(
                ApplicationErrors.InvalidSearchQuery,
                PlaylistErrorMessages.InvalidSearchQuery);
        }

        if (term.Length > MaxQueryLength)
        {
            return Result.Failure<IReadOnlyList<TrackDto>>(
                ApplicationErrors.InvalidSearchQuery,
                PlaylistErrorMessages.SearchQueryTooLong);
        }

        try
        {
            var tracks = await _catalog.SearchTracksAsync(term, cancellationToken);
            return Result.Success(tracks);
        }
        catch (SpotifyUnavailableException)
        {
            return Result.Failure<IReadOnlyList<TrackDto>>(
                ApplicationErrors.SpotifyUnavailable,
                PlaylistErrorMessages.SpotifyUnavailable);
        }
    }
}
