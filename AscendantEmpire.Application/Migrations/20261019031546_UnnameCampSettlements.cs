using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UnnameCampSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Settlement_Game_Name",
                schema: "settlement",
                table: "Settlements");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "settlement",
                table: "Settlements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_Game_Name",
                schema: "settlement",
                table: "Settlements",
                columns: new[] { "GameId", "Name" },
                unique: true,
                filter: "[Name] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Settlement_Game_Name",
                schema: "settlement",
                table: "Settlements");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "settlement",
                table: "Settlements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_Game_Name",
                schema: "settlement",
                table: "Settlements",
                columns: new[] { "GameId", "Name" },
                unique: true);
        }
    }
}
