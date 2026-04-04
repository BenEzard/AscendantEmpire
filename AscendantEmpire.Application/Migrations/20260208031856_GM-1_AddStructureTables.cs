using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM1_AddStructureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Players_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignMapIndex_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_Players_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_GameIndex_GameId",
                schema: "game",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnRoster_Players_PlayerId",
                schema: "game",
                table: "TurnRoster");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_GameId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                schema: "game",
                table: "Players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CampaignMapIndex",
                schema: "mapgallery",
                table: "CampaignMapIndex");

            migrationBuilder.EnsureSchema(
                name: "structure");

            migrationBuilder.RenameTable(
                name: "Players",
                schema: "game",
                newName: "Player",
                newSchema: "game");

            migrationBuilder.RenameTable(
                name: "CampaignMapIndex",
                schema: "mapgallery",
                newName: "CampaignMapGalleryIndex",
                newSchema: "mapgallery");

            migrationBuilder.RenameIndex(
                name: "IX_Players_GameId",
                schema: "game",
                table: "Player",
                newName: "IX_Player_GameId");

            migrationBuilder.AddColumn<int>(
                name: "ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Player",
                schema: "game",
                table: "Player",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CampaignMapGalleryIndex",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex",
                column: "Id");

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
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    ForceId = table.Column<int>(type: "int", nullable: true),
                    SightRange = table.Column<int>(type: "int", nullable: false),
                    CreatedOnTurnId = table.Column<int>(type: "int", nullable: false),
                    InactiveOnTurnId = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Structures",
                schema: "structure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresSlotType = table.Column<int>(type: "int", nullable: false),
                    AllowedPositions = table.Column<int>(type: "int", nullable: false),
                    SightRange = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Structures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StructureInstances",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    BattleMapTileId = table.Column<int>(type: "int", nullable: true),
                    StructureId = table.Column<int>(type: "int", nullable: false),
                    SlotType = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    OwnedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    CreatedOnTurnId = table.Column<int>(type: "int", nullable: false),
                    CompletionPercent = table.Column<double>(type: "float", nullable: false),
                    CompletedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    RemovedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_StructureInstances_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureInstances_Player_OwnedByPlayerId",
                        column: x => x.OwnedByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureInstances_Structures_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "structure",
                        principalTable: "Structures",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerKnownStructures",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: false),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: false),
                    ObservedCompletionPercent = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerKnownStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_StructureInstances_StructureInstanceId",
                        column: x => x.StructureInstanceId,
                        principalSchema: "game",
                        principalTable: "StructureInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap",
                columns: ["GameId", "ControlledByPlayerId"]);

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMap_Game_X_Y",
                schema: "game",
                table: "CampaignMap",
                columns: ["GameId", "XCoord", "YCoord"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_LastSeen",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: ["GameId", "PlayerId", "LastSeenOnTurnId"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_Tile",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: ["GameId", "PlayerId", "CampaignMapId"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_PlayerId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_StructureInstanceId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "StructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerKnownStructures_Game_Player_Instance",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: ["GameId", "PlayerId", "StructureInstanceId"],
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Owner_BattleMap",
                schema: "game",
                table: "StructureInstances",
                columns: ["GameId", "OwnedByPlayerId", "BattleMapTileId"]);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Owner_RemovedOn",
                schema: "game",
                table: "StructureInstances",
                columns: ["GameId", "OwnedByPlayerId", "RemovedOnTurnId"]);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_RemovedOn",
                schema: "game",
                table: "StructureInstances",
                columns: ["GameId", "RemovedOnTurnId"]);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Tile",
                schema: "game",
                table: "StructureInstances",
                columns: ["GameId", "CampaignMapTileId"]);

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_CampaignMapTileId_SlotType",
                schema: "game",
                table: "StructureInstances",
                columns: ["CampaignMapTileId", "SlotType"],
                unique: true,
                filter: "[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_OwnedByPlayerId",
                schema: "game",
                table: "StructureInstances",
                column: "OwnedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_StructureId",
                schema: "game",
                table: "StructureInstances",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_CampaignStructureInstances_Active_BattleTile",
                schema: "game",
                table: "StructureInstances",
                column: "BattleMapTileId",
                unique: true,
                filter: "[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignMapGalleryIndex_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGalleryIndexId",
                principalSchema: "mapgallery",
                principalTable: "CampaignMapGalleryIndex",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Player_GameIndex_GameId",
                schema: "game",
                table: "Player",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_Player_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnRoster_Player_PlayerId",
                schema: "game",
                table: "TurnRoster",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignMapGalleryIndex_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.DropForeignKey(
                name: "FK_Player_GameIndex_GameId",
                schema: "game",
                table: "Player");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_Player_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnRoster_Player_PlayerId",
                schema: "game",
                table: "TurnRoster");

            migrationBuilder.DropTable(
                name: "PlayerKnownStructures",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerTroopSummaries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "StructureInstances",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Structures",
                schema: "structure");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropIndex(
                name: "UX_CampaignMap_Game_X_Y",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Player",
                schema: "game",
                table: "Player");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CampaignMapGalleryIndex",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex");

            migrationBuilder.DropColumn(
                name: "ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameTable(
                name: "Player",
                schema: "game",
                newName: "Players",
                newSchema: "game");

            migrationBuilder.RenameTable(
                name: "CampaignMapGalleryIndex",
                schema: "mapgallery",
                newName: "CampaignMapIndex",
                newSchema: "mapgallery");

            migrationBuilder.RenameIndex(
                name: "IX_Player_GameId",
                schema: "game",
                table: "Players",
                newName: "IX_Players_GameId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                schema: "game",
                table: "Players",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CampaignMapIndex",
                schema: "mapgallery",
                table: "CampaignMapIndex",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_GameId",
                schema: "game",
                table: "CampaignMap",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Players_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignMapIndex_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGalleryIndexId",
                principalSchema: "mapgallery",
                principalTable: "CampaignMapIndex",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_Players_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_GameIndex_GameId",
                schema: "game",
                table: "Players",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnRoster_Players_PlayerId",
                schema: "game",
                table: "TurnRoster",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
