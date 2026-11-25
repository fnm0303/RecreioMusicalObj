using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelas ferramentas do EF Core (migrations), evitando que o host da
/// aplicação seja iniciado só para gerar o esquema.
/// </summary>
public sealed class PlaylistDbContextFactory : IDesignTimeDbContextFactory<PlaylistDbContext>
{
    public PlaylistDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlaylistDbContext>()
            .UseSqlite("Data Source=recreio-musical.db")
            .Options;

        return new PlaylistDbContext(options);
    }
}
