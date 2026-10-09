using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillSnap.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenamePortfolioUserNameToUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "PortfolioUsers",
                newName: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioUsers_UserName",
                table: "PortfolioUsers",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioUsers_UserName",
                table: "PortfolioUsers");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "PortfolioUsers",
                newName: "Name");
        }
    }
}
