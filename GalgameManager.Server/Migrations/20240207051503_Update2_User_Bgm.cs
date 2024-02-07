using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalgameManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update2_User_Bgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BangumiId",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BangumiId",
                table: "User");
        }
    }
}
