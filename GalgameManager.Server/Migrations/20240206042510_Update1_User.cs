using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalgameManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update1_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarLoc",
                table: "User",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarLoc",
                table: "User");
        }
    }
}
