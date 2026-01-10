using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS23_AdjustField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameColumn(
                name: "LastSeenOnTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastVisitedOnTurnId");

            migrationBuilder.RenameColumn(
                name: "LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastVisitedByPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_LastVisitedByPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameColumn(
                name: "LastVisitedOnTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastSeenOnTurnId");

            migrationBuilder.RenameColumn(
                name: "LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastSeenByPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_LastSeenByPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");
        }
    }
}
