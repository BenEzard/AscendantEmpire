using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class G2_AddTurnQueueTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "battle");

            migrationBuilder.EnsureSchema(
                name: "turns");

            migrationBuilder.AddColumn<string>(
                name: "TroopName",
                schema: "units",
                table: "TroopInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Battle",
                schema: "battle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    GlobalBattleName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CommencedAtRoundNumber = table.Column<int>(type: "int", nullable: false),
                    OriginalTileStatus = table.Column<int>(type: "int", nullable: false),
                    OriginalTileOwnerId = table.Column<int>(type: "int", nullable: true),
                    ConcludedAtRoundNumber = table.Column<int>(type: "int", nullable: true),
                    VictorId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Battle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Battle_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Battle_Player_OriginalTileOwnerId",
                        column: x => x.OriginalTileOwnerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Battle_Player_VictorId",
                        column: x => x.VictorId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BattleParticipant",
                schema: "battle",
                columns: table => new
                {
                    BattleId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    JoinedAtRoundNumber = table.Column<int>(type: "int", nullable: false),
                    IsStillInBattle = table.Column<bool>(type: "bit", nullable: false),
                    LeftAtRoundNumber = table.Column<int>(type: "int", nullable: true),
                    ExitReason = table.Column<int>(type: "int", nullable: false),
                    PlayerBattleName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    WasSupplied = table.Column<bool>(type: "bit", nullable: false),
                    IsSupplied = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    PlayerId1 = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleParticipant", x => new { x.BattleId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_BattleParticipant_Battle_BattleId",
                        column: x => x.BattleId,
                        principalSchema: "battle",
                        principalTable: "Battle",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleParticipant_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleParticipant_Player_PlayerId1",
                        column: x => x.PlayerId1,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnQueue",
                schema: "turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    QueueType = table.Column<int>(type: "int", nullable: false),
                    BattleId = table.Column<int>(type: "int", nullable: true),
                    CurrentRoundNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentTurnStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentScheduledTurnEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentActualTurnEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnQueue_Battle_BattleId",
                        column: x => x.BattleId,
                        principalSchema: "battle",
                        principalTable: "Battle",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnQueue_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TurnQueueParticipant",
                schema: "turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnQueueId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TurnOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActiveInLane = table.Column<bool>(type: "bit", nullable: false),
                    LastCompletedRoundNumber = table.Column<int>(type: "int", nullable: true),
                    LastTurnStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTurnEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnQueueParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnQueueParticipant_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnQueueParticipant_TurnQueue_TurnQueueId",
                        column: x => x.TurnQueueId,
                        principalSchema: "turns",
                        principalTable: "TurnQueue",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Battle_GameId",
                schema: "battle",
                table: "Battle",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Battle_OriginalTileOwnerId",
                schema: "battle",
                table: "Battle",
                column: "OriginalTileOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Battle_VictorId",
                schema: "battle",
                table: "Battle",
                column: "VictorId");

            migrationBuilder.CreateIndex(
                name: "UX_Battle_Game_Tile_Open",
                schema: "battle",
                table: "Battle",
                columns: new[] { "GameId", "CampaignMapTileId" },
                unique: true,
                filter: "[ConcludedAtRoundNumber] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_BattleId_IsStillInBattle",
                schema: "battle",
                table: "BattleParticipant",
                columns: new[] { "BattleId", "IsStillInBattle" });

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_PlayerId_IsStillInBattle",
                schema: "battle",
                table: "BattleParticipant",
                columns: new[] { "PlayerId", "IsStillInBattle" });

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_PlayerId1",
                schema: "battle",
                table: "BattleParticipant",
                column: "PlayerId1");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueue_GameId",
                schema: "turns",
                table: "TurnQueue",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueue_GameId_QueueType_IsCompleted",
                schema: "turns",
                table: "TurnQueue",
                columns: new[] { "GameId", "QueueType", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "UX_TurnQueue_Battle",
                schema: "turns",
                table: "TurnQueue",
                column: "BattleId",
                unique: true,
                filter: "[BattleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TurnQueue_Campaign_Game_Active",
                schema: "turns",
                table: "TurnQueue",
                columns: new[] { "GameId", "QueueType" },
                unique: true,
                filter: "[QueueType] = 0 AND [BattleId] IS NULL AND [IsCompleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_PlayerId",
                schema: "turns",
                table: "TurnQueueParticipant",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_IsActiveInLane_TurnOrder",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "IsActiveInLane", "TurnOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_PlayerId",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_Status",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_TurnOrder",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "TurnOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleParticipant",
                schema: "battle");

            migrationBuilder.DropTable(
                name: "TurnQueueParticipant",
                schema: "turns");

            migrationBuilder.DropTable(
                name: "TurnQueue",
                schema: "turns");

            migrationBuilder.DropTable(
                name: "Battle",
                schema: "battle");

            migrationBuilder.DropColumn(
                name: "TroopName",
                schema: "units",
                table: "TroopInstances");
        }
    }
}
