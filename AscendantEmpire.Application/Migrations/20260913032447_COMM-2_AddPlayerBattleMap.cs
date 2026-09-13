using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class COMM2_AddPlayerBattleMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerBattleMap",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    BattleMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    BattleMapIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    AltitudeLevel = table.Column<int>(type: "int", nullable: true),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBattleMap", x => new { x.PlayerId, x.BattleMapId });
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_BattleMapIndex_BattleMapIndexId",
                        column: x => x.BattleMapIndexId,
                        principalSchema: "battle",
                        principalTable: "BattleMapIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_BattleMap_BattleMapId",
                        column: x => x.BattleMapId,
                        principalSchema: "battle",
                        principalTable: "BattleMap",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_PlayerTurn_FirstSeenOnTurnId",
                        column: x => x.FirstSeenOnTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_PlayerTurn_LastSeenOnTurnId",
                        column: x => x.LastSeenOnTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerBattleMap_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_BattleMapId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "BattleMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_BattleMapIndexId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "BattleMapIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_BiomeTypeId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_FirstSeenOnTurnId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "FirstSeenOnTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_GameId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_LastSeenOnTurnId",
                schema: "game",
                table: "PlayerBattleMap",
                column: "LastSeenOnTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerBattleMap_PlayerId_BattleMapIndexId",
                schema: "game",
                table: "PlayerBattleMap",
                columns: new[] { "PlayerId", "BattleMapIndexId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerBattleMap",
                schema: "game");
        }
    }
}
