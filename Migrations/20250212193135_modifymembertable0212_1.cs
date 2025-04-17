using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleLogin.Migrations
{
    /// <inheritdoc />
    public partial class modifymembertable0212_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "userIdx",
                table: "TbMembers",
                newName: "email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "email",
                table: "TbMembers",
                newName: "userIdx");
        }
    }
}
