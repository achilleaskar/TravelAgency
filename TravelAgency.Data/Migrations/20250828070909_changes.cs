using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelAgency.Data.Migrations
{
    /// <inheritdoc />
    public partial class changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "AllotmentRoomTypes");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "AllotmentRoomTypes");

            migrationBuilder.DropColumn(
                name: "IsSpecific",
                table: "AllotmentRoomTypes");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "AllotmentRoomTypes",
                newName: "QuantityTotal");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "AllotmentRoomTypes",
                type: "longblob",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(6)",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerNight",
                table: "AllotmentRoomTypes",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "AllotmentRoomTypes",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(3)",
                oldMaxLength: 3)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "QuantityCancelled",
                table: "AllotmentRoomTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantityCancelled",
                table: "AllotmentRoomTypes");

            migrationBuilder.RenameColumn(
                name: "QuantityTotal",
                table: "AllotmentRoomTypes",
                newName: "Quantity");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RowVersion",
                table: "AllotmentRoomTypes",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "longblob",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerNight",
                table: "AllotmentRoomTypes",
                type: "decimal(12,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "AllotmentRoomTypes",
                type: "varchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "AllotmentRoomTypes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "AllotmentRoomTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpecific",
                table: "AllotmentRoomTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
