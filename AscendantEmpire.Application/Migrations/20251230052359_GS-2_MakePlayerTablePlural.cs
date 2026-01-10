using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS2_MakePlayerTablePlural : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_Player_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Player",
                schema: "game",
                table: "Player");

            migrationBuilder.RenameTable(
                name: "Player",
                schema: "game",
                newName: "Players",
                newSchema: "game");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                schema: "game",
                table: "Players",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                schema: "game",
                table: "Players",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_Players_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_LastSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_Players_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                schema: "game",
                table: "Players");

            migrationBuilder.RenameTable(
                name: "Players",
                schema: "game",
                newName: "Player",
                newSchema: "game");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "game",
                table: "Player",
                newName: "PlayerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Player",
                schema: "game",
                table: "Player",
                column: "PlayerId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_Player_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
