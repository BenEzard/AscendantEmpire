using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class F26_AddBattleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.AddColumn<bool>(
                name: "SideJoinedInView",
                schema: "game",
                table: "PlayerReport",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SideLeftInView",
                schema: "game",
                table: "PlayerReport",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectBattleId",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectTroopsLost",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BattleHistory",
                schema: "battle",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    BattleId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    ParticipantPlayerId = table.Column<int>(type: "int", nullable: true),
                    ExitReason = table.Column<int>(type: "int", nullable: true),
                    TroopsLost = table.Column<int>(type: "int", nullable: true),
                    GroundHeldByPlayerId = table.Column<int>(type: "int", nullable: true),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    PlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    TurnSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleHistory", x => x.Id);
                    table.CheckConstraint("CK_BattleHistory_ExitReasonOnlyWhenParticipantLeft", "([EventType] = 3 AND [ExitReason] IS NOT NULL) OR ([EventType] <> 3 AND [ExitReason] IS NULL)");
                    table.CheckConstraint("CK_BattleHistory_GroundHeldOnlyWhenConcluded", "[EventType] = 4 OR [GroundHeldByPlayerId] IS NULL");
                    table.CheckConstraint("CK_BattleHistory_ParticipantOnlyOnParticipantEvents", "([EventType] IN (2, 3) AND [ParticipantPlayerId] IS NOT NULL) OR ([EventType] NOT IN (2, 3) AND [ParticipantPlayerId] IS NULL)");
                    table.CheckConstraint("CK_BattleHistory_TroopsLostNotNegative", "[TroopsLost] IS NULL OR [TroopsLost] >= 0");
                    table.CheckConstraint("CK_BattleHistory_TroopsLostOnlyWhenParticipantLeft", "([EventType] = 3 AND [TroopsLost] IS NOT NULL) OR ([EventType] <> 3 AND [TroopsLost] IS NULL)");
                    table.ForeignKey(
                        name: "FK_BattleHistory_Battle_BattleId",
                        column: x => x.BattleId,
                        principalSchema: "battle",
                        principalTable: "Battle",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleHistory_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_BattleHistory_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleHistory_PlayerTurn_PlayerTurnId",
                        column: x => x.PlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleHistory_Player_GroundHeldByPlayerId",
                        column: x => x.GroundHeldByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleHistory_Player_ParticipantPlayerId",
                        column: x => x.ParticipantPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectBattleId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectBattleId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectGroundHeldByPlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerReport_Turn_Recipient_Battle",
                schema: "game",
                table: "PlayerReport",
                columns: new[] { "RaisedDuringPlayerTurnId", "RecipientPlayerId", "SubjectBattleId" },
                unique: true,
                filter: "[SubjectBattleId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport",
                sql: "(CASE WHEN [SubjectUnitInstanceId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SubjectStructureInstanceId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SubjectBattleId] IS NULL THEN 0 ELSE 1 END) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_Battle_Turn_Sequence",
                schema: "battle",
                table: "BattleHistory",
                columns: new[] { "BattleId", "PlayerTurnId", "TurnSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_CampaignMapTileId",
                schema: "battle",
                table: "BattleHistory",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_GameId",
                schema: "battle",
                table: "BattleHistory",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_GroundHeldByPlayerId",
                schema: "battle",
                table: "BattleHistory",
                column: "GroundHeldByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_ParticipantPlayerId",
                schema: "battle",
                table: "BattleHistory",
                column: "ParticipantPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleHistory_Turn_Sequence",
                schema: "battle",
                table: "BattleHistory",
                columns: new[] { "PlayerTurnId", "TurnSequence" });

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerReport_Battle_SubjectBattleId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectBattleId",
                principalSchema: "battle",
                principalTable: "Battle",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerReport_Player_SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectGroundHeldByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerReport_Battle_SubjectBattleId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerReport_Player_SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropTable(
                name: "BattleHistory",
                schema: "battle");

            migrationBuilder.DropIndex(
                name: "IX_PlayerReport_SubjectBattleId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropIndex(
                name: "IX_PlayerReport_SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropIndex(
                name: "UX_PlayerReport_Turn_Recipient_Battle",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SideJoinedInView",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SideLeftInView",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectBattleId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectGroundHeldByPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectTroopsLost",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport",
                sql: "[SubjectUnitInstanceId] IS NULL OR [SubjectStructureInstanceId] IS NULL");
        }
    }
}
