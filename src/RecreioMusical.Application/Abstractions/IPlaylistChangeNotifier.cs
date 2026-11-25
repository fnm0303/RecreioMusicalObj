using RecreioMusical.Application.Models;

namespace RecreioMusical.Application.Abstractions;

/// <summary>Avisa os clientes conectados de que a playlist mudou.</summary>
public interface IPlaylistChangeNotifier
{
    Task PublishAsync(
        PlaylistDto playlist,
        CancellationToken cancellationToken = default);
}
