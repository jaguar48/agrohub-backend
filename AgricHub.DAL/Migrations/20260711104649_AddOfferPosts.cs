using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsultantId",
                table: "CustomOffers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferPostId",
                table: "CustomOffers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchMessage",
                table: "CustomOffers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortfolioFileName",
                table: "CustomOffers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortfolioUrl",
                table: "CustomOffers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfferPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PreferredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfferPosts_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OfferPosts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomOffers_OfferPostId",
                table: "CustomOffers",
                column: "OfferPostId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferPosts_CategoryId",
                table: "OfferPosts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferPosts_CustomerId",
                table: "OfferPosts",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomOffers_OfferPosts_OfferPostId",
                table: "CustomOffers",
                column: "OfferPostId",
                principalTable: "OfferPosts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomOffers_OfferPosts_OfferPostId",
                table: "CustomOffers");

            migrationBuilder.DropTable(
                name: "OfferPosts");

            migrationBuilder.DropIndex(
                name: "IX_CustomOffers_OfferPostId",
                table: "CustomOffers");

            migrationBuilder.DropColumn(
                name: "ConsultantId",
                table: "CustomOffers");

            migrationBuilder.DropColumn(
                name: "OfferPostId",
                table: "CustomOffers");

            migrationBuilder.DropColumn(
                name: "PitchMessage",
                table: "CustomOffers");

            migrationBuilder.DropColumn(
                name: "PortfolioFileName",
                table: "CustomOffers");

            migrationBuilder.DropColumn(
                name: "PortfolioUrl",
                table: "CustomOffers");
        }
    }
}
