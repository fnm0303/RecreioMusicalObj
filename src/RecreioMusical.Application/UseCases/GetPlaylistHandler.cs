using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Application.UseCases;

/// <summary>Lê o estado atual da playlist, já ordenado por vaga.</summary>
public sealed class GetPlaylistHandler
{
    private readonly IPlaylistRepository _repository;

    public GetPlaylistHandler(IPlaylistRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<PlaylistDto> HandleAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return PlaylistDto.From(items);
    }
}
