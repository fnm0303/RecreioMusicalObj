using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RecreioMusical.Infrastructure.Persistence;

namespace RecreioMusical.Tests.Infrastructure;

/// <summary>Banco SQLite em memória, mantido aberto durante todo o teste.</summary>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(
        SqliteConnection connection,
        DbContextOptions<PlaylistDbContext> options)
    {
        _connection = connection;
        Options = options;
    }

    public DbContextOptions<PlaylistDbContext> Options { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PlaylistDbContext>()
            .UseSqlite(connection)
            .Options;

        var database = new SqliteTestDatabase(connection, options);

        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return database;
    }

    public PlaylistDbContext CreateContext() => new(Options);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
