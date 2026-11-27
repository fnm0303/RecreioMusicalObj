using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecreioMusical.Application.Abstractions;
using RecreioMusical.Infrastructure.Persistence;
using RecreioMusical.Infrastructure.Realtime;
using RecreioMusical.Infrastructure.Spotify;

namespace RecreioMusical.Infrastructure;

/// <summary>
/// Ponto de composição da infraestrutura: SQLite, catálogo do Spotify e canal de
/// notificações da playlist.
/// </summary>
public static class DependencyInjection
{
    private const string DefaultConnectionString = "Data Source=recreio-musical.db";

    /// <summary>
    /// Registra configuração externa, banco, repositório, Spotify e notificações.
    /// </summary>
    /// <param name="services">Contêiner da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <param name="contentRootPath">
    /// Diretório usado para resolver caminhos relativos do arquivo SQLite.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        services.Configure<SpotifyOptions>(configuration.GetSection(SpotifyOptions.SectionName));

        var connectionString = ResolveConnectionString(
            configuration.GetConnectionString("Playlist"),
            contentRootPath);
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        services.AddSingleton(new SqliteDatabaseOptions
        {
            ConnectionString = connectionString,
            DataSource = dataSource
        });

        services.AddSingleton<SqliteConnectionInterceptor>();
        services.AddDbContext<PlaylistDbContext>((provider, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(provider.GetRequiredService<SqliteConnectionInterceptor>());
        });
        services.AddScoped<IPlaylistRepository, EfPlaylistRepository>();

        services.AddHttpClient(SpotifyHttpClient.Name, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RecreioMusical/1.0");
        });

        services.AddSingleton<ISpotifyAccessTokenProvider, SpotifyTokenProvider>();
        services.AddSingleton<ISpotifyCatalogService, SpotifyCatalogService>();

        services.AddSingleton<PlaylistChangeHub>();
        services.AddSingleton<IPlaylistChangeNotifier>(
            provider => provider.GetRequiredService<PlaylistChangeHub>());

        return services;
    }

    private static string ResolveConnectionString(string? configured, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(configured ?? DefaultConnectionString);

        if (!string.IsNullOrWhiteSpace(builder.DataSource) &&
            builder.DataSource != ":memory:" &&
            !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(contentRootPath, builder.DataSource);
        }

        EnsureDataSourceDirectory(builder.DataSource);

        return builder.ConnectionString;
    }

    private static void EnsureDataSourceDirectory(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource) ||
            dataSource == ":memory:" ||
            dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
