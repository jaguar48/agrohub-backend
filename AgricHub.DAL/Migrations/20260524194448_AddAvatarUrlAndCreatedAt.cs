using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgricHub.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarUrlAndCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Customers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

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

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "StateId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "BusinessName",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Consultants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Consultants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConsultantId1",
                table: "Businesses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_ConsultantId1",
                table: "Wallets",
                column: "ConsultantId1",
                unique: true,
                filter: "[ConsultantId1] IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "IX_Wallets_ConsultantId1",
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
                name: "AvatarUrl",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ConsultantId1",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "CustomerId1",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Consultants");

            migrationBuilder.DropColumn(
                name: "ConsultantId1",
                table: "Businesses");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StateId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CountryId",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BusinessName",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Consultants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
