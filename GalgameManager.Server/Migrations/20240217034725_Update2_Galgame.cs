using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalgameManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update2_Galgame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Galgame");

            migrationBuilder.AddColumn<long>(
                name: "ReleaseDateTimeStamp",
                table: "Galgame",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReleaseDateTimeStamp",
                table: "Galgame");

            migrationBuilder.AddColumn<string>(
                name: "ReleaseDate",
                table: "Galgame",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }
    }
}
