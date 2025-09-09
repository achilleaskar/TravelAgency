using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelAgency.Data.Migrations
{
    /// <inheritdoc />
    public partial class fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChangedAt",
                table: "UpdateLogs",
                newName: "ChangedAtUtc");

            migrationBuilder.RenameColumn(
                name: "DatePolicy",
                table: "Allotments",
                newName: "AllotmentDatePolicy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChangedAtUtc",
                table: "UpdateLogs",
                newName: "ChangedAt");

            migrationBuilder.RenameColumn(
                name: "AllotmentDatePolicy",
                table: "Allotments",
                newName: "DatePolicy");
        }
    }
}
