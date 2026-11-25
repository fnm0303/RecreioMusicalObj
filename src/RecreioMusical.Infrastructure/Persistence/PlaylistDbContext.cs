using Microsoft.EntityFrameworkCore;
using RecreioMusical.Domain;

namespace RecreioMusical.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core da playlist. O SQLite garante no banco as duas invariantes
/// críticas: uma música por vaga (chave primária) e nenhuma faixa repetida
/// (índice único em <see cref="PlaylistItemEntity.TrackId"/>).
/// </summary>
public sealed class PlaylistDbContext : DbContext
{
    public PlaylistDbContext(DbContextOptions<PlaylistDbContext> options)
        : base(options)
    {
    }

    internal DbSet<PlaylistItemEntity> PlaylistItems => Set<PlaylistItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PlaylistItemEntity>(entity =>
        {
            entity.ToTable("PlaylistItems", table =>
                table.HasCheckConstraint(
                    "CK_PlaylistItems_Slot",
                    $"\"Slot\" BETWEEN {PlaylistRules.FirstSlot} AND {PlaylistRules.LastSlot}"));

            entity.HasKey(item => item.Slot);
            entity.Property(item => item.Slot).ValueGeneratedNever();

            entity.Property(item => item.TrackId)
                .HasMaxLength(TrackInfo.MaxIdLength)
                .IsRequired();

            entity.Property(item => item.Title)
                .HasMaxLength(TrackInfo.MaxTitleLength)
                .IsRequired();

            entity.Property(item => item.Artists)
                .HasMaxLength(TrackInfo.MaxArtistsLength)
                .IsRequired();

            entity.Property(item => item.AlbumName)
                .HasMaxLength(TrackInfo.MaxAlbumLength)
                .IsRequired();

            entity.Property(item => item.CoverUrl)
                .HasMaxLength(TrackInfo.MaxUrlLength);

            entity.Property(item => item.SpotifyUrl)
                .HasMaxLength(TrackInfo.MaxUrlLength)
                .IsRequired();

            entity.Property(item => item.AddedBy)
                .HasMaxLength(Nickname.MaxLength)
                .IsRequired();

            entity.HasIndex(item => item.TrackId)
                .IsUnique()
                .HasDatabaseName("IX_PlaylistItems_TrackId");
        });
    }
}
