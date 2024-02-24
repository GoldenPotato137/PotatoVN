using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GalgameManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AvatarLoc = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastGalChangedTimeStamp = table.Column<long>(type: "bigint", nullable: false),
                    BangumiId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Galgame",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LastChangedTimeStamp = table.Column<long>(type: "bigint", nullable: false),
                    BgmId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VndbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2500)", maxLength: 2500, nullable: false),
                    Developer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedPlayTime = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rating = table.Column<float>(type: "real", nullable: false),
                    ReleaseDateTimeStamp = table.Column<long>(type: "bigint", nullable: false),
                    ImageLoc = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: true),
                    TotalPlayTime = table.Column<int>(type: "integer", nullable: false),
                    PlayType = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MyRate = table.Column<int>(type: "integer", nullable: false),
                    PrivateComment = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galgame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Galgame_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GalgameDeleted",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GalgameId = table.Column<int>(type: "integer", nullable: false),
                    DeleteTimeStamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalgameDeleted", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalgameDeleted_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryGalgame",
                columns: table => new
                {
                    CategoriesId = table.Column<int>(type: "integer", nullable: false),
                    GalgamesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryGalgame", x => new { x.CategoriesId, x.GalgamesId });
                    table.ForeignKey(
                        name: "FK_CategoryGalgame_Category_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryGalgame_Galgame_GalgamesId",
                        column: x => x.GalgamesId,
                        principalTable: "Galgame",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GalPlayLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GalgameId = table.Column<int>(type: "integer", nullable: false),
                    DateTimeStamp = table.Column<long>(type: "bigint", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalPlayLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalPlayLog_Galgame_GalgameId",
                        column: x => x.GalgameId,
                        principalTable: "Galgame",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryGalgame_GalgamesId",
                table: "CategoryGalgame",
                column: "GalgamesId");

            migrationBuilder.CreateIndex(
                name: "IX_GalPlayLog_GalgameId",
                table: "GalPlayLog",
                column: "GalgameId");

            migrationBuilder.CreateIndex(
                name: "IX_Galgame_UserId",
                table: "Galgame",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GalgameDeleted_UserId",
                table: "GalgameDeleted",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryGalgame");

            migrationBuilder.DropTable(
                name: "GalPlayLog");

            migrationBuilder.DropTable(
                name: "GalgameDeleted");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "Galgame");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
