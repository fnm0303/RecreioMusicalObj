using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecreioMusical.Application;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Application.UseCases;
using RecreioMusical.Infrastructure;
using RecreioMusical.Infrastructure.Realtime;

namespace RecreioMusical.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Application_and_infrastructure_compose_a_valid_container()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Playlist"] = "Data Source=:memory:",
                ["Spotify:Market"] = "BR"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration, Path.GetTempPath());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchTracksHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetPlaylistHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AddTrackHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RemoveTrackHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ClearPlaylistHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPlaylistRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISpotifyCatalogService>());

        var hub = scope.ServiceProvider.GetRequiredService<PlaylistChangeHub>();
        Assert.Same(hub, scope.ServiceProvider.GetRequiredService<IPlaylistChangeNotifier>());
    }
}
