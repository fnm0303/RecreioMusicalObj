namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Caminho efetivo do banco SQLite, compartilhado com as rotinas de manutenção
/// (backup) que não têm acesso ao <see cref="PlaylistDbContext"/>.
/// </summary>
public sealed class SqliteDatabaseOptions
{
    public required string ConnectionString { get; init; }

    /// <summary>Caminho absoluto do arquivo ou <c>:memory:</c>.</summary>
    public required string DataSource { get; init; }
}
