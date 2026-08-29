using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Moves campaign map tile ids from IDENTITY to application-assigned values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tile's id is <c>offset + XCoord + YCoord * MapWidth</c>. That only holds if the application
    /// chooses the value: an IDENTITY column numbers rows in whatever order the query plan emits
    /// them, which for these tables was not the coordinate order every map read assumes.
    /// </para>
    /// <para>
    /// SQL Server cannot ALTER the IDENTITY property off an existing column, so both tables are
    /// dropped and recreated. EF scaffolded this as an AlterColumn, which would have failed at
    /// runtime; the body below is written by hand. Every inbound foreign key has to be dropped and
    /// restored around the rebuild.
    /// </para>
    /// <para>
    /// Run <c>scripts/CM-25-reset-games.sql</c> first. The guard at the top of <c>Up</c> stops the
    /// migration if that has not happened, rather than dropping live game data.
    /// </para>
    /// </remarks>
    public partial class ApplicationAssignedCampaignMapTileIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse to run against populated tables. The rebuild drops them, and the rows could not
            // be carried over anyway: their ids were assigned in an order the new scheme does not use.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [game].[CampaignMap])
                   OR EXISTS (SELECT 1 FROM [game].[PlayerCampaignMap])
                BEGIN
                    THROW 50000, 'game.CampaignMap / game.PlayerCampaignMap still hold rows. Run scripts/CM-25-reset-games.sql before applying this migration.', 1;
                END
                ");

            // --- Detach everything pointing at CampaignMap -----------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_BattleMapIndex_CampaignMap_CampaignMapId",
                schema: "battle",
                table: "BattleMapIndex");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                schema: "game",
                table: "StructureInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances");

            // PlayerCampaignMap goes first: its own FK to CampaignMap disappears with it.
            migrationBuilder.DropTable(
                name: "PlayerCampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "CampaignMap",
                schema: "game");

            // --- Recreate CampaignMap, this time without IDENTITY ----------------------------
            migrationBuilder.CreateTable(
                name: "CampaignMap",
                schema: "game",
                columns: table => new
                {
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    ForageAmount = table.Column<int>(type: "int", nullable: true),
                    HuntAmount = table.Column<int>(type: "int", nullable: true),
                    FishAmount = table.Column<int>(type: "int", nullable: true),
                    FodderAmount = table.Column<int>(type: "int", nullable: true),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenByPlayerId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedByPlayerId = table.Column<int>(type: "int", nullable: true),
                    ControlledByPlayerId = table.Column<int>(type: "int", nullable: true),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMap", x => x.CampaignMapId);
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_ControlledByPlayerId",
                        column: x => x.ControlledByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                        column: x => x.FirstSeenByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_LastVisitedByPlayerId",
                        column: x => x.LastVisitedByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            // --- Recreate PlayerCampaignMap, likewise without IDENTITY -----------------------
            migrationBuilder.CreateTable(
                name: "PlayerCampaignMap",
                schema: "game",
                columns: table => new
                {
                    PlayerCampaignMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCampaignMap", x => x.PlayerCampaignMapId);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // --- Indexes ---------------------------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_BiomeTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "ControlledByPlayerId" });

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMap_Game_X_Y",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "XCoord", "YCoord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_BiomeTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_GameId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId");

            // --- Reattach everything that points at CampaignMap ------------------------------
            migrationBuilder.AddForeignKey(
                name: "FK_BattleMapIndex_CampaignMap_CampaignMapId",
                schema: "battle",
                table: "BattleMapIndex",
                column: "CampaignMapId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "CampaignMapId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                schema: "game",
                table: "StructureInstances",
                column: "CampaignMapTileId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances",
                column: "CampaignMapTileId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");

            // --- The game's own map offset and dimensions ------------------------------------
            migrationBuilder.AddColumn<int>(
                name: "CampaignMapOffsetId",
                schema: "game",
                table: "GameIndex",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MapWidth",
                schema: "game",
                table: "GameIndex",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MapHeight",
                schema: "game",
                table: "GameIndex",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The same shape in reverse: both tables are rebuilt with their IDENTITY property
            // restored. Any rows created under the new scheme go with them, exactly as Up drops
            // the rows created under the old one.
            migrationBuilder.DropColumn(
                name: "CampaignMapOffsetId",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropColumn(
                name: "MapWidth",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropColumn(
                name: "MapHeight",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropForeignKey(
                name: "FK_BattleMapIndex_CampaignMap_CampaignMapId",
                schema: "battle",
                table: "BattleMapIndex");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                schema: "game",
                table: "StructureInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropTable(
                name: "PlayerCampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "CampaignMap",
                schema: "game");

            migrationBuilder.CreateTable(
                name: "CampaignMap",
                schema: "game",
                columns: table => new
                {
                    CampaignMapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    ForageAmount = table.Column<int>(type: "int", nullable: true),
                    HuntAmount = table.Column<int>(type: "int", nullable: true),
                    FishAmount = table.Column<int>(type: "int", nullable: true),
                    FodderAmount = table.Column<int>(type: "int", nullable: true),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenByPlayerId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedByPlayerId = table.Column<int>(type: "int", nullable: true),
                    ControlledByPlayerId = table.Column<int>(type: "int", nullable: true),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMap", x => x.CampaignMapId);
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_ControlledByPlayerId",
                        column: x => x.ControlledByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                        column: x => x.FirstSeenByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_LastVisitedByPlayerId",
                        column: x => x.LastVisitedByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerCampaignMap",
                schema: "game",
                columns: table => new
                {
                    PlayerCampaignMapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCampaignMap", x => x.PlayerCampaignMapId);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_BiomeTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "ControlledByPlayerId" });

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMap_Game_X_Y",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "XCoord", "YCoord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_BiomeTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_GameId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BattleMapIndex_CampaignMap_CampaignMapId",
                schema: "battle",
                table: "BattleMapIndex",
                column: "CampaignMapId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "CampaignMapId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                schema: "game",
                table: "StructureInstances",
                column: "CampaignMapTileId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances",
                column: "CampaignMapTileId",
                principalSchema: "game",
                principalTable: "CampaignMap",
                principalColumn: "CampaignMapId");
        }
    }
}
