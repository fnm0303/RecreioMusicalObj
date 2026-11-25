using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecreioMusical.Infrastructure.Spotify;

namespace RecreioMusical.Infrastructure;

/// <summary>
/// Ponto de composição da infraestrutura.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra configuração externa e, nas próximas fases, persistência e HTTP.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SpotifyOptions>(configuration.GetSection(SpotifyOptions.SectionName));

        // EF Core/SQLite, token e catálogo do Spotify entram na Fase 3.
        return services;
    }
}
