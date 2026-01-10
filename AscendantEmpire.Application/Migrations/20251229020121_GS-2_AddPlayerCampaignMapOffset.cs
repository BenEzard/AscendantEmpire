using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS2_AddPlayerCampaignMapOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_FirstVisitedPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_LastVisitedPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameColumn(
                name: "LastVisitedTurnId",
                schema: "game",
                table: "PlayerCampaignMap",
                newName: "LastSeenOnTurnId");

            migrationBuilder.RenameColumn(
                name: "FirstVisitedTurnId",
                schema: "game",
                table: "PlayerCampaignMap",
                newName: "FirstSeenOnTurnId");

            migrationBuilder.RenameColumn(
                name: "LastVisitedTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastSeenOnTurnId");

            migrationBuilder.RenameColumn(
                name: "LastVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastSeenByPlayerId");

            migrationBuilder.RenameColumn(
                name: "FirstVisitedTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "FirstSeenOnTurnId");

            migrationBuilder.RenameColumn(
                name: "FirstVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "FirstSeenByPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_LastVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_LastSeenByPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_FirstVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_FirstSeenByPlayerId");

            migrationBuilder.AddColumn<int>(
                name: "CampaignOffsetId",
                schema: "game",
                table: "Player",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "CampaignOffsetId",
                schema: "game",
                table: "Player");

            migrationBuilder.RenameColumn(
                name: "LastSeenOnTurnId",
                schema: "game",
                table: "PlayerCampaignMap",
                newName: "LastVisitedTurnId");

            migrationBuilder.RenameColumn(
                name: "FirstSeenOnTurnId",
                schema: "game",
                table: "PlayerCampaignMap",
                newName: "FirstVisitedTurnId");

            migrationBuilder.RenameColumn(
                name: "LastSeenOnTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastVisitedTurnId");

            migrationBuilder.RenameColumn(
                name: "LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "LastVisitedPlayerId");

            migrationBuilder.RenameColumn(
                name: "FirstSeenOnTurnId",
                schema: "game",
                table: "CampaignMap",
                newName: "FirstVisitedTurnId");

            migrationBuilder.RenameColumn(
                name: "FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "FirstVisitedPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_LastVisitedPlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "IX_CampaignMap_FirstVisitedPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_FirstVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstVisitedPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_LastVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "PlayerId");
        }
    }
}
