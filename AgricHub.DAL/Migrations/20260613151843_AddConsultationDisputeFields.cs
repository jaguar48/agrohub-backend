using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationDisputeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeRaisedAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "Consultations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeStatus",
                table: "Consultations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisputeRaisedAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "DisputeStatus",
                table: "Consultations");
        }
    }
}
