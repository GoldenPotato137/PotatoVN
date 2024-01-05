using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalgameManager.Core.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Galgames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    SavePath = table.Column<string>(type: "TEXT", nullable: true),
                    ExePath = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    RunAsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    BgmId = table.Column<string>(type: "TEXT", nullable: true),
                    VndbId = table.Column<string>(type: "TEXT", nullable: true),
                    MixedId = table.Column<string>(type: "TEXT", nullable: true),
                    RssType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CnName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Developer = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedPlayTime = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<float>(type: "REAL", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LastPlay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalPlayTime = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayType = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    MyRate = table.Column<int>(type: "INTEGER", nullable: false),
                    PrivateComment = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galgames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryGalgame",
                columns: table => new
                {
                    CategoriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    GalgamesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryGalgame", x => new { x.CategoriesId, x.GalgamesId });
                    table.ForeignKey(
                        name: "FK_CategoryGalgame_Categories_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryGalgame_Galgames_GalgamesId",
                        column: x => x.GalgamesId,
                        principalTable: "Galgames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GalTag",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GalgameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalTag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalTag_Galgames_GalgameId",
                        column: x => x.GalgameId,
                        principalTable: "Galgames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GalgameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Minute = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayLog_Galgames_GalgameId",
                        column: x => x.GalgameId,
                        principalTable: "Galgames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryGalgame_GalgamesId",
                table: "CategoryGalgame",
                column: "GalgamesId");

            migrationBuilder.CreateIndex(
                name: "IX_GalTag_GalgameId",
                table: "GalTag",
                column: "GalgameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayLog_GalgameId",
                table: "PlayLog",
                column: "GalgameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryGalgame");

            migrationBuilder.DropTable(
                name: "GalTag");

            migrationBuilder.DropTable(
                name: "PlayLog");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Galgames");
        }
    }
}
