using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class F24_AddSightingReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerKnownForce",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitInstanceId = table.Column<int>(type: "int", nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    HealthyTroops = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Fidelity = table.Column<int>(type: "int", nullable: false),
                    LastSeenCampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    LastSeenPlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    LastSeenTurnSequence = table.Column<int>(type: "int", nullable: false),
                    ExitDirection = table.Column<int>(type: "int", nullable: true),
                    StillVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerKnownForce", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_CampaignMap_LastSeenCampaignMapTileId",
                        column: x => x.LastSeenCampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_PlayerTurn_LastSeenPlayerTurnId",
                        column: x => x.LastSeenPlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_Player_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownForce_UnitTemplate_UnitTemplateId",
                        column: x => x.UnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerReport",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    RecipientPlayerId = table.Column<int>(type: "int", nullable: false),
                    RaisedDuringPlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    SubjectUnitInstanceId = table.Column<int>(type: "int", nullable: true),
                    SubjectOwnerPlayerId = table.Column<int>(type: "int", nullable: true),
                    SubjectUnitTemplateId = table.Column<int>(type: "int", nullable: true),
                    SubjectHealthyTroops = table.Column<int>(type: "int", nullable: true),
                    Fidelity = table.Column<int>(type: "int", nullable: false),
                    FirstCampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    LastCampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    FirstTurnSequence = table.Column<int>(type: "int", nullable: false),
                    LastTurnSequence = table.Column<int>(type: "int", nullable: false),
                    ExitDirection = table.Column<int>(type: "int", nullable: true),
                    StillVisible = table.Column<bool>(type: "bit", nullable: false),
                    AppearedInView = table.Column<bool>(type: "bit", nullable: false),
                    DestroyedInView = table.Column<bool>(type: "bit", nullable: false),
                    TrackOrdinal = table.Column<int>(type: "int", nullable: false),
                    FirstWitnessId = table.Column<int>(type: "int", nullable: false),
                    FirstWitnessRecordType = table.Column<int>(type: "int", nullable: false),
                    FirstWitnessOwnerPlayerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerReport_CampaignMap_FirstCampaignMapTileId",
                        column: x => x.FirstCampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_PlayerReport_CampaignMap_LastCampaignMapTileId",
                        column: x => x.LastCampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_PlayerReport_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerReport_PlayerTurn_RaisedDuringPlayerTurnId",
                        column: x => x.RaisedDuringPlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerReport_Player_FirstWitnessOwnerPlayerId",
                        column: x => x.FirstWitnessOwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerReport_Player_RecipientPlayerId",
                        column: x => x.RecipientPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerReport_Player_SubjectOwnerPlayerId",
                        column: x => x.SubjectOwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerReport_UnitTemplate_SubjectUnitTemplateId",
                        column: x => x.SubjectUnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerReportPathTile",
                schema: "game",
                columns: table => new
                {
                    PlayerReportId = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerReportPathTile", x => new { x.PlayerReportId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_PlayerReportPathTile_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_PlayerReportPathTile_PlayerReport_PlayerReportId",
                        column: x => x.PlayerReportId,
                        principalSchema: "game",
                        principalTable: "PlayerReport",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownForce_LastSeenCampaignMapTileId",
                schema: "game",
                table: "PlayerKnownForce",
                column: "LastSeenCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownForce_LastSeenPlayerTurnId",
                schema: "game",
                table: "PlayerKnownForce",
                column: "LastSeenPlayerTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownForce_OwnerPlayerId",
                schema: "game",
                table: "PlayerKnownForce",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownForce_PlayerId",
                schema: "game",
                table: "PlayerKnownForce",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownForce_UnitTemplateId",
                schema: "game",
                table: "PlayerKnownForce",
                column: "UnitTemplateId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerKnownForce_Game_Player_Unit",
                schema: "game",
                table: "PlayerKnownForce",
                columns: new[] { "GameId", "PlayerId", "UnitInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_FirstCampaignMapTileId",
                schema: "game",
                table: "PlayerReport",
                column: "FirstCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_FirstWitnessOwnerPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "FirstWitnessOwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_LastCampaignMapTileId",
                schema: "game",
                table: "PlayerReport",
                column: "LastCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_Recipient_Turn",
                schema: "game",
                table: "PlayerReport",
                columns: new[] { "GameId", "RecipientPlayerId", "RaisedDuringPlayerTurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_RecipientPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "RecipientPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectOwnerPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectOwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectUnitTemplateId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectUnitTemplateId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerReport_Turn_Recipient_Subject_Track",
                schema: "game",
                table: "PlayerReport",
                columns: new[] { "RaisedDuringPlayerTurnId", "RecipientPlayerId", "SubjectUnitInstanceId", "TrackOrdinal" },
                unique: true,
                filter: "[SubjectUnitInstanceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReportPathTile_CampaignMapTileId",
                schema: "game",
                table: "PlayerReportPathTile",
                column: "CampaignMapTileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerKnownForce",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerReportPathTile",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerReport",
                schema: "game");
        }
    }
}
