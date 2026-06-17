using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerNoShowGraceHours",
                table: "Consultations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerNoShowProcessed",
                table: "Consultations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerNoShowRequestedAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoShowGraceHours",
                table: "Consultations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoShowProcessed",
                table: "Consultations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoShowRequestedAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerNoShowGraceHours",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CustomerNoShowProcessed",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CustomerNoShowRequestedAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "NoShowGraceHours",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "NoShowProcessed",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "NoShowRequestedAt",
                table: "Consultations");
        }
    }
}
