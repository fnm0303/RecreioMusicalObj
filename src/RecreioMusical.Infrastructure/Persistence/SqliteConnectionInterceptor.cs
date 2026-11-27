using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Ajusta cada conexão do SQLite para tolerar a latência do armazenamento de rede
/// do App Service (<c>/home</c>), esperando por um lock em vez de falhar na hora.
/// </summary>
internal sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMilliseconds = 10_000;

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        Configure(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ConfigureAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static void Configure(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
        command.ExecuteNonQuery();
    }

    private static async Task ConfigureAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
