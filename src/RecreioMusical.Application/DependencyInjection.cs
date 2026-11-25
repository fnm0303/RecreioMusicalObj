using Microsoft.Extensions.DependencyInjection;

namespace RecreioMusical.Application;

/// <summary>
/// Ponto de composição da camada de aplicação.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra os casos de uso da aplicação.
    /// Os handlers da playlist e da busca entram aqui na Fase 2.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
