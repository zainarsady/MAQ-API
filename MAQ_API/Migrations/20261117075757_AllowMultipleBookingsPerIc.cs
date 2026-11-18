using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAQ_API.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleBookingsPerIc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TempahanRamadans_ICNumber_BookedDate",
                table: "TempahanRamadans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TempahanRamadans_ICNumber_BookedDate",
                table: "TempahanRamadans",
                columns: new[] { "ICNumber", "BookedDate" },
                unique: true);
        }
    }
}
