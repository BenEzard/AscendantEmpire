using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Adds the structure feed of the intelligence and sightings design: <c>structure.StructureHistory</c>, Structure subjects on <c>game.PlayerReport</c>, and a rebuilt
    /// <c>game.PlayerKnownStructure</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-edited.</b> The scaffolder treated the known-structure change as a rename of <c>PlayerKnownStructures</c>: it renamed <c>LastSeenOnTurnId</c> onto <c>StructureId</c> and
    /// <c>CampaignMapId</c> onto <c>Status</c> because their types matched, and added the other new non-null columns with <c>defaultValue:</c>, which SQL Server turns into unnamed <c>DF__</c>
    /// constraints - the clutter TM-6 had to clean up. The old table has never been written by anything, so <see cref="Up"/> simply drops it and creates the new one from scratch.
    /// <see cref="Down"/> recreates the old table exactly as InitialCreate made it, empty.
    /// </para>
    /// <para>
    /// The <c>PlayerReport</c> columns are all nullable - null on a Force report - so adding them needs no default constraints either. The model snapshot and designer are as scaffolded.
    /// </para>
    /// </remarks>
    public partial class F25_AddStructureHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- The old known-structure table ---------------------------------------------------
            // Never written by anything, so there is nothing to carry across. Dropping the table takes its keys and indexes with it.
            migrationBuilder.DropTable(
                name: "PlayerKnownStructures",
                schema: "game");

            // --- Structure subjects on the report feed -------------------------------------------
            migrationBuilder.AddColumn<bool>(
                name: "CapturedInView",
                schema: "game",
                table: "PlayerReport",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompletedInView",
                schema: "game",
                table: "PlayerReport",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SubjectCompletionPercent",
                schema: "game",
                table: "PlayerReport",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectStructureId",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport",
                type: "int",
                nullable: true);

            // --- The structure feed --------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "StructureHistory",
                schema: "structure",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: false),
                    StructureId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    PreviousOwnerPlayerId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    CompletionPercent = table.Column<double>(type: "float", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    PlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    TurnSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureHistory", x => x.Id);
                    table.CheckConstraint("CK_StructureHistory_PreviousOwnerOnlyWhenCaptured", "([EventType] = 4 AND [PreviousOwnerPlayerId] IS NOT NULL) OR ([EventType] <> 4 AND [PreviousOwnerPlayerId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_StructureHistory_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_StructureHistory_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureHistory_PlayerTurn_PlayerTurnId",
                        column: x => x.PlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureHistory_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureHistory_Player_PreviousOwnerPlayerId",
                        column: x => x.PreviousOwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureHistory_StructureInstances_StructureInstanceId",
                        column: x => x.StructureInstanceId,
                        principalSchema: "game",
                        principalTable: "StructureInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureHistory_Structures_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "structure",
                        principalTable: "Structures",
                        principalColumn: "Id");
                });

            // --- The rebuilt known-structure table ------------------------------------------------
            // Created from scratch rather than altered, so no column needs a default. Every relationship is NoAction with a name, where the old table's tile relationship was an unnamed Cascade.
            migrationBuilder.CreateTable(
                name: "PlayerKnownStructure",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: false),
                    StructureId = table.Column<int>(type: "int", nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: false),
                    ObservedCompletionPercent = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Fidelity = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    LastSeenPlayerTurnId = table.Column<int>(type: "int", nullable: false),
                    LastSeenTurnSequence = table.Column<int>(type: "int", nullable: false),
                    StillVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerKnownStructure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_PlayerTurn_LastSeenPlayerTurnId",
                        column: x => x.LastSeenPlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_Player_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_StructureInstances_StructureInstanceId",
                        column: x => x.StructureInstanceId,
                        principalSchema: "game",
                        principalTable: "StructureInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructure_Structures_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "structure",
                        principalTable: "Structures",
                        principalColumn: "Id");
                });

            // --- Indexes and constraints ---------------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectPreviousOwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectStructureId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReport_SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerReport_Turn_Recipient_Structure",
                schema: "game",
                table: "PlayerReport",
                columns: new[] { "RaisedDuringPlayerTurnId", "RecipientPlayerId", "SubjectStructureInstanceId" },
                unique: true,
                filter: "[SubjectStructureInstanceId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport",
                sql: "[SubjectUnitInstanceId] IS NULL OR [SubjectStructureInstanceId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_CampaignMapTileId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_LastSeenPlayerTurnId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "LastSeenPlayerTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_OwnerPlayerId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "OwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_PlayerId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_StructureId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructure_StructureInstanceId",
                schema: "game",
                table: "PlayerKnownStructure",
                column: "StructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerKnownStructure_Game_Player_Structure",
                schema: "game",
                table: "PlayerKnownStructure",
                columns: new[] { "GameId", "PlayerId", "StructureInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_CampaignMapTileId",
                schema: "structure",
                table: "StructureHistory",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_GameId",
                schema: "structure",
                table: "StructureHistory",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_PlayerId",
                schema: "structure",
                table: "StructureHistory",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_PreviousOwnerPlayerId",
                schema: "structure",
                table: "StructureHistory",
                column: "PreviousOwnerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_Structure_Turn_Sequence",
                schema: "structure",
                table: "StructureHistory",
                columns: new[] { "StructureInstanceId", "PlayerTurnId", "TurnSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_StructureId",
                schema: "structure",
                table: "StructureHistory",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureHistory_Turn_Sequence",
                schema: "structure",
                table: "StructureHistory",
                columns: new[] { "PlayerTurnId", "TurnSequence" });

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerReport_Player_SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectPreviousOwnerPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerReport_StructureInstances_SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectStructureInstanceId",
                principalSchema: "game",
                principalTable: "StructureInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerReport_Structures_SubjectStructureId",
                schema: "game",
                table: "PlayerReport",
                column: "SubjectStructureId",
                principalSchema: "structure",
                principalTable: "Structures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerReport_Player_SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerReport_StructureInstances_SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerReport_Structures_SubjectStructureId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropTable(
                name: "StructureHistory",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "PlayerKnownStructure",
                schema: "game");

            migrationBuilder.DropIndex(
                name: "IX_PlayerReport_SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropIndex(
                name: "IX_PlayerReport_SubjectStructureId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropIndex(
                name: "IX_PlayerReport_SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropIndex(
                name: "UX_PlayerReport_Turn_Recipient_Structure",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerReport_OneSubject",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "CapturedInView",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "CompletedInView",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectCompletionPercent",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectPreviousOwnerPlayerId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectStructureId",
                schema: "game",
                table: "PlayerReport");

            migrationBuilder.DropColumn(
                name: "SubjectStructureInstanceId",
                schema: "game",
                table: "PlayerReport");

            // The old table, exactly as InitialCreate made it - including its Cascade to the tile - and as empty as it was.
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
                name: "IX_PlayerKnownStructures_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_LastSeen",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: new[] { "GameId", "PlayerId", "LastSeenOnTurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_Tile",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: new[] { "GameId", "PlayerId", "CampaignMapId" });

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
                columns: new[] { "GameId", "PlayerId", "StructureInstanceId" },
                unique: true);
        }
    }
}
