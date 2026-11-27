using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Cria uma cópia diária do SQLite dentro do armazenamento persistente do App
/// Service (<c>/home/data/backups</c>), mantendo as sete mais recentes.
/// </summary>
public sealed class SqliteBackupService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private const int RetainedBackups = 7;
    private const string FilePrefix = "recreio-musical-";

    private readonly SqliteDatabaseOptions _database;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SqliteBackupService> _logger;

    public SqliteBackupService(
        SqliteDatabaseOptions database,
        IHostEnvironment environment,
        ILogger<SqliteBackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _database = database;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogDebug("Backup automático do SQLite desativado em Development.");
            return;
        }

        try
        {
            await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await CreateBackupAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do host.
        }
    }

    private async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_database.DataSource) ||
                _database.DataSource == ":memory:" ||
                !File.Exists(_database.DataSource))
            {
                _logger.LogWarning(
                    "Backup ignorado: o arquivo SQLite '{DataSource}' não existe.",
                    _database.DataSource);
                return;
            }

            var databaseDirectory = Path.GetDirectoryName(_database.DataSource);
            if (string.IsNullOrWhiteSpace(databaseDirectory))
            {
                _logger.LogWarning("Backup ignorado: diretório do SQLite não pôde ser resolvido.");
                return;
            }

            var backupDirectory = Path.Combine(databaseDirectory, "backups");
            Directory.CreateDirectory(backupDirectory);

            var destination = Path.Combine(
                backupDirectory,
                $"{FilePrefix}{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");

            await using var connection = new SqliteConnection(_database.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = connection.CreateCommand();
            command.CommandText = $"VACUUM INTO '{Escape(destination)}';";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            Prune(backupDirectory);
            _logger.LogInformation("Backup do SQLite criado em {Path}.", destination);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao criar o backup do SQLite.");
        }
    }

    private void Prune(string backupDirectory)
    {
        var expired = Directory
            .GetFiles(backupDirectory, $"{FilePrefix}*.db")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Skip(RetainedBackups);

        foreach (var path in expired)
        {
            File.Delete(path);
        }
    }

    private static string Escape(string path) => path.Replace("'", "''", StringComparison.Ordinal);
}
