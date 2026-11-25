using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Tests.Fakes;

internal sealed class RecordingPlaylistNotifier : IPlaylistChangeNotifier
{
    public List<PlaylistDto> Published { get; } = [];

    public Task PublishAsync(
        PlaylistDto playlist,
        CancellationToken cancellationToken = default)
    {
        Published.Add(playlist);
        return Task.CompletedTask;
    }
}
