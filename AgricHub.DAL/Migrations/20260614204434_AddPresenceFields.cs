using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPresenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Consultants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Consultants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Consultants");
        }
    }
}
