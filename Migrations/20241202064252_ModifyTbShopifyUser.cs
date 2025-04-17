using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleLogin.Migrations
{
    /// <inheritdoc />
    public partial class ModifyTbShopifyUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "User_Id",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address1",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address2",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdAt",
                table: "TbShopifyUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province_code",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedAt",
                table: "TbShopifyUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zip",
                table: "TbShopifyUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "User_Id",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "address1",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "address2",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "city",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "country",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "createdAt",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "province",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "province_code",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "updatedAt",
                table: "TbShopifyUsers");

            migrationBuilder.DropColumn(
                name: "zip",
                table: "TbShopifyUsers");
        }
    }
}
