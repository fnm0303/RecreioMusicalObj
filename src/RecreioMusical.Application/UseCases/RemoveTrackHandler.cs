using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.Models;
using RecreioMusical.Domain;

namespace RecreioMusical.Application.UseCases;

public sealed record RemoveTrackCommand(int Slot);

/// <summary>Remove uma música da playlist, liberando a vaga correspondente.</summary>
public sealed class RemoveTrackHandler
{
    private readonly IPlaylistRepository _repository;
    private readonly IPlaylistChangeNotifier _notifier;

    public RemoveTrackHandler(
        IPlaylistRepository repository,
        IPlaylistChangeNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(notifier);

        _repository = repository;
        _notifier = notifier;
    }

    public async Task<Result> HandleAsync(
        RemoveTrackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!PlaylistRules.IsValidSlot(command.Slot))
        {
            return Result.Failure(
                PlaylistErrors.InvalidSlot,
                PlaylistErrorMessages.InvalidSlot);
        }

        var removed = await _repository.RemoveAsync(command.Slot, cancellationToken);
        if (!removed)
        {
            return Result.Failure(
                ApplicationErrors.ItemNotFound,
                PlaylistErrorMessages.ItemNotFound);
        }

        await PublishPlaylistAsync(cancellationToken);
        return Result.Success();
    }

    private async Task PublishPlaylistAsync(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        await _notifier.PublishAsync(PlaylistDto.From(items), cancellationToken);
    }
}
