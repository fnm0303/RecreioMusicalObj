using System.Collections.Concurrent;
using System.Threading.Channels;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.Models;

namespace RecreioMusical.Infrastructure.Realtime;

/// <summary>
/// Mantém o último snapshot da playlist e distribui as mudanças para os clientes
/// conectados. O endpoint SSE da camada web apenas assina este hub.
/// </summary>
public sealed class PlaylistChangeHub : IPlaylistChangeNotifier
{
    private const int SubscriberBuffer = 8;

    private readonly ConcurrentDictionary<Guid, Channel<PlaylistDto>> _subscribers = new();
    private readonly object _gate = new();

    private PlaylistDto _current = new([]);

    /// <summary>Último estado publicado, usado como evento inicial de uma conexão.</summary>
    public PlaylistDto Current => Volatile.Read(ref _current);

    public Task PublishAsync(
        PlaylistDto playlist,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        lock (_gate)
        {
            Volatile.Write(ref _current, playlist);

            foreach (var channel in _subscribers.Values)
            {
                channel.Writer.TryWrite(playlist);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Assina as próximas mudanças. O primeiro item lido é sempre o snapshot atual,
    /// para que a tela abra já sincronizada.
    /// </summary>
    public PlaylistSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<PlaylistDto>(new BoundedChannelOptions(SubscriberBuffer)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        lock (_gate)
        {
            _subscribers[id] = channel;
            channel.Writer.TryWrite(_current);
        }

        return new PlaylistSubscription(channel.Reader, () => Unsubscribe(id));
    }

    private void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}

/// <summary>Conexão de leitura de um cliente; descartá-la cancela a assinatura.</summary>
public sealed class PlaylistSubscription : IAsyncDisposable
{
    private readonly ChannelReader<PlaylistDto> _reader;
    private readonly Action _unsubscribe;
    private bool _disposed;

    internal PlaylistSubscription(ChannelReader<PlaylistDto> reader, Action unsubscribe)
    {
        _reader = reader;
        _unsubscribe = unsubscribe;
    }

    public IAsyncEnumerable<PlaylistDto> ReadAllAsync(CancellationToken cancellationToken) =>
        _reader.ReadAllAsync(cancellationToken);

    /// <summary>Espera assíncrona usada pelo SSE para mesclar leituras e heartbeats.</summary>
    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        _reader.WaitToReadAsync(cancellationToken);

    public bool TryRead(out PlaylistDto playlist) =>
        _reader.TryRead(out playlist!);

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _unsubscribe();
        }

        return ValueTask.CompletedTask;
    }
}
