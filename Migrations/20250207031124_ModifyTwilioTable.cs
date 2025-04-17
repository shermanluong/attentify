using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleLogin.Migrations
{
    /// <inheritdoc />
    public partial class ModifyTwilioTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "TbTwilios",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "TbTwilios");
        }
    }
}
