using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM3_AddStructureImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullImagePath",
                schema: "structure",
                table: "Structures",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MapImagePath",
                schema: "structure",
                table: "Structures",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RecordTypeId",
                schema: "structure",
                table: "Structures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SightDirection",
                schema: "structure",
                table: "Structures",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances",
                column: "CampaignMapTileId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances",
                column: "CampaignMapTileId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropIndex(
                name: "IX_TroopInstances_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropColumn(
                name: "FullImagePath",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "MapImagePath",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "RecordTypeId",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "SightDirection",
                schema: "structure",
                table: "Structures");
        }
    }
}
