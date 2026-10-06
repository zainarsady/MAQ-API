using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MAQ_API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TempahanRamadans",
                columns: table => new
                {
                    TempahanRamadanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BookedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CustomerName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ICNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Age = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempahanRamadans", x => x.TempahanRamadanId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "TempahanRamadans",
                columns: new[] { "TempahanRamadanId", "Address", "Age", "Amount", "BookedDate", "ContactNumber", "CreatedAt", "CustomerName", "ICNumber" },
                values: new object[,]
                {
                    { 1, "No 1, Jalan Contoh, 50000 Kuala Lumpur", 36, 350.00m, new DateOnly(2027, 2, 20), "0123456789", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ahmad bin Ali", "900101-10-1234" },
                    { 2, "No 2, Jalan Contoh, 40000 Shah Alam", 41, 500.00m, new DateOnly(2027, 2, 21), "0198765432", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Siti binti Hassan", "850505-14-5678" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TempahanRamadans_BookedDate",
                table: "TempahanRamadans",
                column: "BookedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TempahanRamadans_ICNumber_BookedDate",
                table: "TempahanRamadans",
                columns: new[] { "ICNumber", "BookedDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TempahanRamadans");
        }
    }
}
