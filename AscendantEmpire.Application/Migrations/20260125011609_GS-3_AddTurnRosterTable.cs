using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS3_AddTurnRosterTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TurnRoster",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TurnOrder = table.Column<int>(type: "int", nullable: false),
                    BattleId = table.Column<int>(type: "int", nullable: true),
                    PlayerStillInBattle = table.Column<bool>(type: "bit", nullable: true),
                    PlayerBattleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TurnStartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TurnEndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledTurnEndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnRoster", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnRoster_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TurnRoster_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TurnRoster_PlayerId",
                schema: "game",
                table: "TurnRoster",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_TurnRoster_Battle_Game_Battle_Player_Round",
                schema: "game",
                table: "TurnRoster",
                columns: ["GameId", "BattleId", "PlayerId", "RoundNumber"],
                unique: true,
                filter: "[BattleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TurnRoster_Campaign_Game_Player_Round",
                schema: "game",
                table: "TurnRoster",
                columns: ["GameId", "PlayerId", "RoundNumber"],
                unique: true,
                filter: "[BattleId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TurnRoster",
                schema: "game");
        }
    }
}
