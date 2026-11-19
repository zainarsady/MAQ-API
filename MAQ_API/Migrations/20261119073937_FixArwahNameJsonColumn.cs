using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MAQ_API.Migrations
{
    /// <inheritdoc />
    public partial class FixArwahNameJsonColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows were written while ArwahName was a plain string ("", "Ahmad", ...).
            // EF now reads this column as a JSON array, so any non-array value blows up
            // materialization ("The empty string is not valid JSON"). Normalise them to NULL.
            migrationBuilder.Sql(
                """
                UPDATE `TempahanRamadans`
                SET `ArwahName` = NULL
                WHERE `ArwahName` IS NOT NULL
                  AND (JSON_VALID(`ArwahName`) = 0 OR JSON_TYPE(`ArwahName`) <> 'ARRAY');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ArwahName",
                table: "TempahanRamadans",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArwahName",
                table: "TempahanRamadans",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
