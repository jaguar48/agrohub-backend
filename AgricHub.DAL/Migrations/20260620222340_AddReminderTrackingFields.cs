using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedStartReminderSentAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueReviewReminderSentAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingApprovalReminderSentAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingCancelNudgeSentAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingReminderSentAt",
                table: "Consultations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedStartReminderSentAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "OverdueReviewReminderSentAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "PendingApprovalReminderSentAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "PendingCancelNudgeSentAt",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "PendingReminderSentAt",
                table: "Consultations");
        }
    }
}
