using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelAgency.Data.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UpdateLogs_EntityType_EntityId_ChangedAt",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Allotments");

            migrationBuilder.DropColumn(
                name: "QuantityCancelled",
                table: "AllotmentRoomTypes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AllotmentRoomTypes");

            migrationBuilder.RenameColumn(
                name: "Field",
                table: "UpdateLogs",
                newName: "PropertyName");

            migrationBuilder.RenameColumn(
                name: "QuantityTotal",
                table: "AllotmentRoomTypes",
                newName: "Quantity");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "UpdateLogs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<int>(
                name: "AllotmentPaymentId",
                table: "UpdateLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllotmentRoomTypeId",
                table: "UpdateLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "UpdateLogs",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Allotments",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DatePolicy",
                table: "Allotments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerNight",
                table: "AllotmentRoomTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AllotmentRoomTypes",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AllotmentRoomTypes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AllotmentRoomTypes",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "AllotmentPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AllotmentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsVoided = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllotmentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllotmentPayments_Allotments_AllotmentId",
                        column: x => x.AllotmentId,
                        principalTable: "Allotments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UpdateLogs_AllotmentPaymentId",
                table: "UpdateLogs",
                column: "AllotmentPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_UpdateLogs_AllotmentRoomTypeId",
                table: "UpdateLogs",
                column: "AllotmentRoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AllotmentPayments_AllotmentId",
                table: "AllotmentPayments",
                column: "AllotmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_UpdateLogs_AllotmentPayments_AllotmentPaymentId",
                table: "UpdateLogs",
                column: "AllotmentPaymentId",
                principalTable: "AllotmentPayments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UpdateLogs_AllotmentRoomTypes_AllotmentRoomTypeId",
                table: "UpdateLogs",
                column: "AllotmentRoomTypeId",
                principalTable: "AllotmentRoomTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UpdateLogs_AllotmentPayments_AllotmentPaymentId",
                table: "UpdateLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_UpdateLogs_AllotmentRoomTypes_AllotmentRoomTypeId",
                table: "UpdateLogs");

            migrationBuilder.DropTable(
                name: "AllotmentPayments");

            migrationBuilder.DropIndex(
                name: "IX_UpdateLogs_AllotmentPaymentId",
                table: "UpdateLogs");

            migrationBuilder.DropIndex(
                name: "IX_UpdateLogs_AllotmentRoomTypeId",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "AllotmentPaymentId",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "AllotmentRoomTypeId",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "UpdateLogs");

            migrationBuilder.DropColumn(
                name: "DatePolicy",
                table: "Allotments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AllotmentRoomTypes");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AllotmentRoomTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AllotmentRoomTypes");

            migrationBuilder.RenameColumn(
                name: "PropertyName",
                table: "UpdateLogs",
                newName: "Field");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "AllotmentRoomTypes",
                newName: "QuantityTotal");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "UpdateLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "UpdateLogs",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "UpdateLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Allotments",
                keyColumn: "Title",
                keyValue: null,
                column: "Title",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Allotments",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "Allotments",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PricePerNight",
                table: "AllotmentRoomTypes",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "QuantityCancelled",
                table: "AllotmentRoomTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AllotmentRoomTypes",
                type: "longblob",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpdateLogs_EntityType_EntityId_ChangedAt",
                table: "UpdateLogs",
                columns: new[] { "EntityType", "EntityId", "ChangedAt" });
        }
    }
}
