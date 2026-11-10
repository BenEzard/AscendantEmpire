using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class MM8_RemovePlayerCultureName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Player_Game_CultureName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "CultureName",
                schema: "game",
                table: "Player");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CultureName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_CI_AS");

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_CultureName",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "CultureName" },
                unique: true);
        }
    }
}
