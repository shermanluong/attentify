using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleLogin.Migrations
{
    /// <inheritdoc />
    public partial class modifytbmailaccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authcode",
                table: "TbMailAccount");

            migrationBuilder.DropColumn(
                name: "clientId",
                table: "TbMailAccount");

            migrationBuilder.DropColumn(
                name: "clientSecret",
                table: "TbMailAccount");

            migrationBuilder.DropColumn(
                name: "redirecUri",
                table: "TbMailAccount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authcode",
                table: "TbMailAccount",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "clientId",
                table: "TbMailAccount",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "clientSecret",
                table: "TbMailAccount",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "redirecUri",
                table: "TbMailAccount",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
