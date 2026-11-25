using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecreioMusical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlaylist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Slot = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Artists = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AlbumName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    SpotifyUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Slot);
                    table.CheckConstraint("CK_PlaylistItems_Slot", "\"Slot\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_TrackId",
                table: "PlaylistItems",
                column: "TrackId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaylistItems");
        }
    }
}
