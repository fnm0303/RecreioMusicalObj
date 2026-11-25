using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Application.UseCases;

/// <summary>Esvazia a playlist inteira (ação administrativa).</summary>
public sealed class ClearPlaylistHandler
{
    private readonly IPlaylistRepository _repository;
    private readonly IPlaylistChangeNotifier _notifier;

    public ClearPlaylistHandler(
        IPlaylistRepository repository,
        IPlaylistChangeNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(notifier);

        _repository = repository;
        _notifier = notifier;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken = default)
    {
        await _repository.ClearAsync(cancellationToken);
        await PublishPlaylistAsync(cancellationToken);
        return Result.Success();
    }

    private async Task PublishPlaylistAsync(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        await _notifier.PublishAsync(PlaylistDto.From(items), cancellationToken);
    }
}
