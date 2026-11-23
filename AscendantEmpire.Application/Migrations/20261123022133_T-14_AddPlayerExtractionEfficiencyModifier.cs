using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class T14_AddPlayerExtractionEfficiencyModifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerExtractionEfficiencyModifier",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerExtractionEfficiencyModifier", x => x.Id);
                    table.CheckConstraint("CK_PlayerExtractionEfficiencyModifier_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0");
                    table.ForeignKey(
                        name: "FK_PlayerExtractionEfficiencyModifier_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerExtractionEfficiencyModifier_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerExtractionEfficiencyModifier_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerExtractionEfficiencyModifier_AssetId",
                schema: "game",
                table: "PlayerExtractionEfficiencyModifier",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerExtractionEfficiencyModifier_GameId",
                schema: "game",
                table: "PlayerExtractionEfficiencyModifier",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerExtractionEfficiencyModifier_Player_Source_Asset",
                schema: "game",
                table: "PlayerExtractionEfficiencyModifier",
                columns: new[] { "PlayerId", "SourceType", "SourceId", "AssetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerExtractionEfficiencyModifier",
                schema: "game");
        }
    }
}
