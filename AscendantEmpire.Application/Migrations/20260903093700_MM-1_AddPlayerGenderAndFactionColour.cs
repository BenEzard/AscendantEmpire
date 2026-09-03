using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class MM1_AddPlayerGenderAndFactionColour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FactionColour",
                schema: "game",
                table: "Player",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RulerGender",
                schema: "game",
                table: "Player",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_FactionColour",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "FactionColour" },
                unique: true,
                filter: "[FactionColour] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Player_Game_FactionColour",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "FactionColour",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "RulerGender",
                schema: "game",
                table: "Player");
        }
    }
}
