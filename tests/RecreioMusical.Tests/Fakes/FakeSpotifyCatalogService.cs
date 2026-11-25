using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Tests.Fakes;

internal sealed class FakeSpotifyCatalogService : ISpotifyCatalogService
{
    public IReadOnlyList<TrackDto> SearchResults { get; set; } = [];

    public TrackDto? TrackResult { get; set; }

    public Exception? SearchFailure { get; set; }

    public Exception? TrackFailure { get; set; }

    public List<string> SearchQueries { get; } = [];

    public List<string> RequestedTrackIds { get; } = [];

    public Task<IReadOnlyList<TrackDto>> SearchTracksAsync(
        string query,
        CancellationToken cancellationToken)
    {
        SearchQueries.Add(query);

        if (SearchFailure is not null)
        {
            throw SearchFailure;
        }

        return Task.FromResult(SearchResults);
    }

    public Task<TrackDto?> GetTrackAsync(string trackId, CancellationToken cancellationToken)
    {
        RequestedTrackIds.Add(trackId);

        if (TrackFailure is not null)
        {
            throw TrackFailure;
        }

        return Task.FromResult(TrackResult);
    }
}
