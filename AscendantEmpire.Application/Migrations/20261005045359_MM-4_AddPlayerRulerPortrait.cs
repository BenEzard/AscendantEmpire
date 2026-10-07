using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Adds RulerPortraitPath, the root-relative path to the portrait a player has chosen for their ruler.
    /// </summary>
    /// <remarks>
    /// Non-nullable, and backfilled with the empty string - which is not a gap but the real "has not chosen a portrait yet" value, the one this column
    /// may hold more than once within a game. Existing rows therefore land in exactly the state game setup now seeds a new slot with, and the game
    /// toolbar draws its crown glyph for them until their owner picks a face. Not applied.
    /// </remarks>
    public partial class MM4_AddPlayerRulerPortrait : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RulerPortraitPath",
                schema: "game",
                table: "Player",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RulerPortraitPath",
                schema: "game",
                table: "Player");
        }
    }
}
