using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleLogin.Migrations
{
    /// <inheritdoc />
    public partial class AddTbSmsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TbSmss",
                columns: table => new
                {
                    sm_idx = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sm_id = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sm_to = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sm_body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sm_from = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sm_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    sm_read = table.Column<int>(type: "int", nullable: true),
                    sm_state = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TbSmss", x => x.sm_idx);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TbSmss");
        }
    }
}
