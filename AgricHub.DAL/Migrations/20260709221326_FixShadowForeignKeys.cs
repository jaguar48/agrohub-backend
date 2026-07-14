using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixShadowForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Businesses_Consultants_ConsultantId1",
                table: "Businesses");

            migrationBuilder.DropForeignKey(
                name: "FK_Consultations_Consultants_ConsultantId1",
                table: "Consultations");

            migrationBuilder.DropForeignKey(
                name: "FK_Consultations_Customers_CustomerId1",
                table: "Consultations");

            migrationBuilder.DropForeignKey(
                name: "FK_Wallets_Consultants_ConsultantId1",
                table: "Wallets");

            migrationBuilder.DropForeignKey(
                name: "FK_Wallets_Customers_CustomerId1",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_ConsultantId",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_ConsultantId1",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_CustomerId",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_CustomerId1",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Consultations_ConsultantId1",
                table: "Consultations");

            migrationBuilder.DropIndex(
                name: "IX_Consultations_CustomerId1",
                table: "Consultations");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_ConsultantId1",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ConsultantId1",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "CustomerId1",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "ConsultantId1",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CustomerId1",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "ConsultantId1",
                table: "Businesses");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_ConsultantId",
                table: "Wallets",
                column: "ConsultantId",
                unique: true,
                filter: "[ConsultantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerId",
                table: "Wallets",
                column: "CustomerId",
                unique: true,
                filter: "[CustomerId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_ConsultantId",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_Wallets_CustomerId",
                table: "Wallets");

            migrationBuilder.AddColumn<int>(
                name: "ConsultantId1",
                table: "Wallets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId1",
                table: "Wallets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsultantId1",
                table: "Consultations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId1",
                table: "Consultations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsultantId1",
                table: "Businesses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_ConsultantId",
                table: "Wallets",
                column: "ConsultantId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_ConsultantId1",
                table: "Wallets",
                column: "ConsultantId1",
                unique: true,
                filter: "[ConsultantId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerId",
                table: "Wallets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerId1",
                table: "Wallets",
                column: "CustomerId1",
                unique: true,
                filter: "[CustomerId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_ConsultantId1",
                table: "Consultations",
                column: "ConsultantId1");

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_CustomerId1",
                table: "Consultations",
                column: "CustomerId1");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_ConsultantId1",
                table: "Businesses",
                column: "ConsultantId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Businesses_Consultants_ConsultantId1",
                table: "Businesses",
                column: "ConsultantId1",
                principalTable: "Consultants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Consultations_Consultants_ConsultantId1",
                table: "Consultations",
                column: "ConsultantId1",
                principalTable: "Consultants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Consultations_Customers_CustomerId1",
                table: "Consultations",
                column: "CustomerId1",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Wallets_Consultants_ConsultantId1",
                table: "Wallets",
                column: "ConsultantId1",
                principalTable: "Consultants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Wallets_Customers_CustomerId1",
                table: "Wallets",
                column: "CustomerId1",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
