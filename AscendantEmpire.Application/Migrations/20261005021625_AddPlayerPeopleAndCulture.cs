using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Adds the four people-and-culture names a player gives their faction: PeopleName, Demonym, CultureName and Language.
    /// </summary>
    /// <remarks>
    /// All four are non-nullable, so existing rows are backfilled with an empty string rather than a made-up name. Game setup seeds a placeholder
    /// into each for new slots, and the setup dialog requires all four before it will save, so a blank only ever survives on a slot from a game that
    /// predates this migration and has not been edited since.
    /// </remarks>
    public partial class AddPlayerPeopleAndCulture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CultureName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Demonym",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PeopleName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CultureName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "Demonym",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "PeopleName",
                schema: "game",
                table: "Player");
        }
    }
}
