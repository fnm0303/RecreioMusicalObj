using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Common;
using RecreioMusical.Application.Models;
using RecreioMusical.Domain;

namespace RecreioMusical.Application.UseCases;

public sealed record AddTrackCommand(string? TrackId, string? AddedBy);

/// <summary>
/// Adiciona uma música à primeira vaga livre. A faixa é reconfirmada no Spotify
/// antes de entrar na playlist e a inserção lida com conflitos de concorrência.
/// </summary>
public sealed class AddTrackHandler
{
    private const int MaxAttempts = 3;

    private readonly IPlaylistRepository _repository;
    private readonly ISpotifyCatalogService _catalog;
    private readonly IPlaylistChangeNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public AddTrackHandler(
        IPlaylistRepository repository,
        ISpotifyCatalogService catalog,
        IPlaylistChangeNotifier notifier,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _catalog = catalog;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PlaylistItemDto>> HandleAsync(
        AddTrackCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trackId = command.TrackId?.Trim() ?? string.Empty;
        if (!SpotifyTrackId.IsValid(trackId))
        {
            return Result.Failure<PlaylistItemDto>(
                ApplicationErrors.InvalidTrackId,
                PlaylistErrorMessages.InvalidTrackId);
        }

        Nickname nickname;
        try
        {
            nickname = Nickname.Create(command.AddedBy);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PlaylistItemDto>(exception.Code, exception.Message);
        }

        if (await _repository.ContainsTrackAsync(trackId, cancellationToken))
        {
            return Result.Failure<PlaylistItemDto>(
                ApplicationErrors.DuplicateTrack,
                PlaylistErrorMessages.DuplicateTrack);
        }

        var occupiedSlots = await _repository.GetOccupiedSlotsAsync(cancellationToken);
        if (PlaylistRules.NextFreeSlot(occupiedSlots) is null)
        {
            return Result.Failure<PlaylistItemDto>(
                ApplicationErrors.PlaylistFull,
                PlaylistErrorMessages.PlaylistFull);
        }

        TrackDto? track;
        try
        {
            track = await _catalog.GetTrackAsync(trackId, cancellationToken);
        }
        catch (SpotifyUnavailableException)
        {
            return Result.Failure<PlaylistItemDto>(
                ApplicationErrors.SpotifyUnavailable,
                PlaylistErrorMessages.SpotifyUnavailable);
        }

        if (track is null)
        {
            return Result.Failure<PlaylistItemDto>(
                ApplicationErrors.TrackNotFound,
                PlaylistErrorMessages.TrackNotFound);
        }

        TrackInfo trackInfo;
        try
        {
            trackInfo = TrackInfo.Create(
                track.Id,
                track.Title,
                track.Artists,
                track.AlbumName,
                track.CoverUrl,
                track.DurationMs,
                track.SpotifyUrl);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PlaylistItemDto>(exception.Code, exception.Message);
        }

        var addedAt = _timeProvider.GetUtcNow();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                occupiedSlots = await _repository.GetOccupiedSlotsAsync(cancellationToken);
            }

            var slot = PlaylistRules.NextFreeSlot(occupiedSlots);
            if (slot is null)
            {
                return Result.Failure<PlaylistItemDto>(
                    ApplicationErrors.PlaylistFull,
                    PlaylistErrorMessages.PlaylistFull);
            }

            var item = PlaylistItem.Place(slot.Value, trackInfo, nickname, addedAt);
            var outcome = await _repository.TryAddAsync(item, cancellationToken);

            if (outcome == AddPlaylistItemOutcome.Added)
            {
                await PublishPlaylistAsync(cancellationToken);
                return Result.Success(PlaylistItemDto.FromDomain(item));
            }

            if (outcome == AddPlaylistItemOutcome.DuplicateTrack)
            {
                return Result.Failure<PlaylistItemDto>(
                    ApplicationErrors.DuplicateTrack,
                    PlaylistErrorMessages.DuplicateTrack);
            }

            // SlotTaken: outra pessoa ocupou a vaga entre a leitura e a inserção.
        }

        return Result.Failure<PlaylistItemDto>(
            ApplicationErrors.PlaylistBusy,
            PlaylistErrorMessages.PlaylistBusy);
    }

    private async Task PublishPlaylistAsync(CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        await _notifier.PublishAsync(PlaylistDto.From(items), cancellationToken);
    }
}
