using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessServicesAndVerif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentPathsJson",
                table: "BusinessVerifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionNotes",
                table: "BusinessVerifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BusinessVerifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "BusinessVerifications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentPathsJson",
                table: "BusinessVerifications");

            migrationBuilder.DropColumn(
                name: "RejectionNotes",
                table: "BusinessVerifications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BusinessVerifications");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "BusinessVerifications");
        }
    }
}
