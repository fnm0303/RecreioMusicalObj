using RecreioMusical.Application.Models;
using RecreioMusical.Infrastructure.Realtime;

namespace RecreioMusical.Tests.Infrastructure;

public sealed class PlaylistChangeHubTests
{
    [Fact]
    public async Task Publish_updates_the_current_snapshot()
    {
        var hub = new PlaylistChangeHub();
        var playlist = PlaylistDto.From([TestData.Item(1), TestData.Item(2)]);

        await hub.PublishAsync(playlist);

        Assert.Same(playlist, hub.Current);
        Assert.Equal(2, hub.Current.Count);
    }

    [Fact]
    public async Task New_subscription_receives_the_current_snapshot_first()
    {
        var hub = new PlaylistChangeHub();
        var playlist = PlaylistDto.From([TestData.Item(1)]);
        await hub.PublishAsync(playlist);

        await using var subscription = hub.Subscribe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = subscription
            .ReadAllAsync(timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.True(await reader.MoveNextAsync());
        Assert.Same(playlist, reader.Current);
    }

    [Fact]
    public async Task Subscription_receives_every_published_snapshot()
    {
        var hub = new PlaylistChangeHub();
        await using var subscription = hub.Subscribe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = subscription
            .ReadAllAsync(timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.True(await reader.MoveNextAsync());
        Assert.Empty(reader.Current.Items);

        var playlist = PlaylistDto.From([TestData.Item(1)]);
        await hub.PublishAsync(playlist);

        Assert.True(await reader.MoveNextAsync());
        Assert.Same(playlist, reader.Current);
    }

    [Fact]
    public async Task Disposed_subscription_stops_receiving_snapshots()
    {
        var hub = new PlaylistChangeHub();
        var subscription = hub.Subscribe();
        await subscription.DisposeAsync();

        await hub.PublishAsync(PlaylistDto.From([TestData.Item(1)]));

        Assert.Equal(1, hub.Current.Count);
    }
}
