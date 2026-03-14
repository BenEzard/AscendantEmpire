using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM2_AdaptExistingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerTroopSummaries",
                schema: "game");

            migrationBuilder.EnsureSchema(
                name: "unitdesigns");

            migrationBuilder.EnsureSchema(
                name: "units");

            migrationBuilder.CreateTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitDesignId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SightRange = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledDesignStats", x => x.UnitDesignId);
                });

            migrationBuilder.CreateTable(
                name: "TroopInstances",
                schema: "units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    BattleMapTileId = table.Column<int>(type: "int", nullable: true),
                    UnitDesignId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopInstances_CompiledDesignStats_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "CompiledDesignStats",
                        principalColumn: "UnitDesignId");
                    table.ForeignKey(
                        name: "FK_TroopInstances_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopInstances_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_GameId",
                schema: "units",
                table: "TroopInstances",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_PlayerId",
                schema: "units",
                table: "TroopInstances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitDesignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TroopInstances",
                schema: "units");

            migrationBuilder.DropTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns");

            migrationBuilder.CreateTable(
                name: "PlayerTroopSummaries",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    BattleMapId = table.Column<int>(type: "int", nullable: true),
                    CreatedOnTurnId = table.Column<int>(type: "int", nullable: false),
                    ForceId = table.Column<int>(type: "int", nullable: true),
                    InactiveOnTurnId = table.Column<int>(type: "int", nullable: false),
                    SightRange = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTroopSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTroopSummaries_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerTroopSummaries_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTroopSummaries_GameId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTroopSummaries_PlayerId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "PlayerId");
        }
    }
}
