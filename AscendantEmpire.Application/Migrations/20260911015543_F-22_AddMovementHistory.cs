using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class F22_AddMovementHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovementHistory",
                schema: "units",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    UnitInstanceId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    FromCampaignMapTileId = table.Column<int>(type: "int", nullable: true),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    PlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    TurnSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementHistory", x => x.Id);
                    table.CheckConstraint("CK_MovementHistory_FromTileOnlyWhenMoved", "([EventType] = 2 AND [FromCampaignMapTileId] IS NOT NULL) OR ([EventType] <> 2 AND [FromCampaignMapTileId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_MovementHistory_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_MovementHistory_CampaignMap_FromCampaignMapTileId",
                        column: x => x.FromCampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_MovementHistory_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovementHistory_PlayerTurn_PlayerTurnId",
                        column: x => x.PlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovementHistory_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MovementHistory_UnitTemplate_UnitTemplateId",
                        column: x => x.UnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_CampaignMapTileId",
                schema: "units",
                table: "MovementHistory",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_FromCampaignMapTileId",
                schema: "units",
                table: "MovementHistory",
                column: "FromCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_GameId",
                schema: "units",
                table: "MovementHistory",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_PlayerId",
                schema: "units",
                table: "MovementHistory",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_Turn_Sequence",
                schema: "units",
                table: "MovementHistory",
                columns: new[] { "PlayerTurnId", "TurnSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_Unit_Turn_Sequence",
                schema: "units",
                table: "MovementHistory",
                columns: new[] { "UnitInstanceId", "PlayerTurnId", "TurnSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_MovementHistory_UnitTemplateId",
                schema: "units",
                table: "MovementHistory",
                column: "UnitTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovementHistory",
                schema: "units");
        }
    }
}
