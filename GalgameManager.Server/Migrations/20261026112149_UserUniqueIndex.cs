using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalgameManager.Server.Migrations
{
    /// <inheritdoc />
    public partial class UserUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_User_BangumiId",
                table: "User",
                column: "BangumiId",
                unique: true,
                filter: "\"BangumiId\" <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_User_UserName",
                table: "User",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_BangumiId",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_UserName",
                table: "User");
        }
    }
}
