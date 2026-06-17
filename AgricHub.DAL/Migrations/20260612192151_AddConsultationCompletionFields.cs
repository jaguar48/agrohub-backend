using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationCompletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletionFileUrl",
                table: "Consultations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletionSubmittedAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionSummary",
                table: "Consultations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionFileUrl",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CompletionSubmittedAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CompletionSummary",
                table: "Consultations");
        }
    }
}
