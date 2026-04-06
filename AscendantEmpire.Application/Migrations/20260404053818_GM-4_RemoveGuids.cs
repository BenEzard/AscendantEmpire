using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM4_RemoveGuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Guid",
                schema: "game",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "GameGuid",
                schema: "game",
                table: "GameIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Guid",
                schema: "game",
                table: "Player",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GameGuid",
                schema: "game",
                table: "GameIndex",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
