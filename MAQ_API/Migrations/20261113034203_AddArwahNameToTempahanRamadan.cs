using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAQ_API.Migrations
{
    /// <inheritdoc />
    public partial class AddArwahNameToTempahanRamadan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArwahName",
                table: "TempahanRamadans",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "TempahanRamadans",
                keyColumn: "TempahanRamadanId",
                keyValue: 1,
                column: "ArwahName",
                value: "");

            migrationBuilder.UpdateData(
                table: "TempahanRamadans",
                keyColumn: "TempahanRamadanId",
                keyValue: 2,
                column: "ArwahName",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArwahName",
                table: "TempahanRamadans");
        }
    }
}
