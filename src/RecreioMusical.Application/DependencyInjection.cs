using Microsoft.Extensions.DependencyInjection;
using RecreioMusical.Application.UseCases;

namespace RecreioMusical.Application;

/// <summary>
/// Ponto de composição da camada de aplicação.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra o relógio e os casos de uso. As implementações de repositório,
    /// catálogo e notificações vêm da camada de infraestrutura.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<SearchTracksHandler>();
        services.AddScoped<GetPlaylistHandler>();
        services.AddScoped<AddTrackHandler>();
        services.AddScoped<RemoveTrackHandler>();
        services.AddScoped<ClearPlaylistHandler>();

        return services;
    }
}
