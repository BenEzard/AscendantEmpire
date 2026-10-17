using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Tile ownership: renames <c>CampaignMap.ControlledByPlayerId</c> to <c>OwnedByPlayerId</c> and adds the rest of the ownership layers, plus <c>game.CampaignMapClaim</c>.
    /// </summary>
    /// <remarks>
    /// <b>Hand-corrected after scaffolding.</b> Six nullable int columns arrive at once, and the scaffolder paired the renamed column with the wrong one - it emitted
    /// <c>RenameColumn ControlledByPlayerId -> RebellionPercentRemaining</c> and created <c>OwnedByPlayerId</c> empty, which would have moved every tile's owner into the
    /// rebellion countdown and dropped the ownership data on the floor. The rename below targets <c>OwnedByPlayerId</c>, as it must, and
    /// <c>RebellionPercentRemaining</c> is added as the new column it is. Re-scaffolding this migration will reintroduce the fault.
    /// <para>
    /// The two indexes on the renamed column are renamed rather than dropped and recreated, for the same reason: there is no new index here, only the old one under a name
    /// that still says what it covers.
    /// </para>
    /// </remarks>
    public partial class CM56_AddTileOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The foreign key goes first and comes back at the end under its new name: SQL Server cannot rename a constraint through sp_rename without the column rename
            // leaving the old name behind, and nothing is stored in a constraint, so dropping it costs no data.
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap");

            // The rename, carrying every existing tile's owner with it.
            migrationBuilder.RenameColumn(
                name: "ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "OwnedByPlayerId");

            // Both indexes already cover the renamed column - SQL Server tracks index columns by id, not by name - so these only correct the names.
            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                newName: "IX_CampaignMap_OwnedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                newName: "IX_CampaignMap_Game_OwnedByPlayer",
                schema: "game",
                table: "CampaignMap");

            // The remaining exclusive layers. All nullable: a tile with nothing but an owner is the ordinary case, and every layer absent is wilderness.
            migrationBuilder.AddColumn<int>(
                name: "OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccupiedSinceTurnId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AbandonedOnTurnId",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RebellionPercentRemaining",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            // How the percentage falls is deliberately not pinned: the design flags the rebellion length as a value to be tuned. 0 is excluded because a tile that has
            // finished rebelling is set back to NULL rather than left at 0.
            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignMap_RebellionPercentRemaining",
                schema: "game",
                table: "CampaignMap",
                sql: "[RebellionPercentRemaining] IS NULL OR [RebellionPercentRemaining] BETWEEN 1 AND 100");

            // Claims are non-exclusive, so they are rows rather than a column, and the table is sparse - one row per Player per claimed tile, nothing for the rest of the map.
            migrationBuilder.CreateTable(
                name: "CampaignMapClaim",
                schema: "game",
                columns: table => new
                {
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapClaim", x => new { x.CampaignMapId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_CampaignMapClaim_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_CampaignMapClaim_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMapClaim_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapClaim_Game_Player",
                schema: "game",
                table: "CampaignMapClaim",
                columns: new[] { "GameId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapClaim_PlayerId",
                schema: "game",
                table: "CampaignMapClaim",
                column: "PlayerId");

            // The conventional index behind each new Player foreign key.
            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "OccupiedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastOwnedByPlayerId");

            // Ownership keys to Player throughout. OwnedBySettlementId gets no foreign key: Settlements are not modelled yet.
            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_OwnedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "OwnedByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "OccupiedByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastOwnedByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Player_OwnedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropTable(
                name: "CampaignMapClaim",
                schema: "game");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignMap_RebellionPercentRemaining",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "RebellionPercentRemaining",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "AbandonedOnTurnId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "LastOwnedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "OccupiedSinceTurnId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "OccupiedByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap");

            // Back to the old names, owners and all. The mirror of Up: rename the indexes back, rename the column back, then restore the foreign key under its old name.
            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_Game_OwnedByPlayer",
                newName: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameIndex(
                name: "IX_CampaignMap_OwnedByPlayerId",
                newName: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.RenameColumn(
                name: "OwnedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                newName: "ControlledByPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Player_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");
        }
    }
}
