using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelGenerator.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldersIsSelected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", nullable: true),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Private",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UpdatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Private", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UpdatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlbumImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbumImages_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumImages_AlbumId",
                table: "AlbumImages",
                column: "AlbumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumImages");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "Private");

            migrationBuilder.DropTable(
                name: "Recent");

            migrationBuilder.DropTable(
                name: "Albums");
        }
    }
}
