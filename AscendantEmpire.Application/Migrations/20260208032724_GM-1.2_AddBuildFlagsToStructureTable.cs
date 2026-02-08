using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM12_AddBuildFlagsToStructureTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanBeBuiltOnBattleMap",
                schema: "structure",
                table: "Structures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanBeBuiltOnCampaignMap",
                schema: "structure",
                table: "Structures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DualMapBuildTrigger",
                schema: "structure",
                table: "Structures",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanBeBuiltOnBattleMap",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "CanBeBuiltOnCampaignMap",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "DualMapBuildTrigger",
                schema: "structure",
                table: "Structures");
        }
    }
}
