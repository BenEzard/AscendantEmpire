using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM38_AddPlayerSustenanceModifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerSustenanceModifier",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Resource = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: true),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSustenanceModifier", x => x.Id);
                    table.CheckConstraint("CK_PlayerSustenanceModifier_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0");
                    table.ForeignKey(
                        name: "FK_PlayerSustenanceModifier_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerSustenanceModifier_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerSustenanceModifier_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSustenanceModifier_BiomeTypeId",
                schema: "game",
                table: "PlayerSustenanceModifier",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSustenanceModifier_GameId",
                schema: "game",
                table: "PlayerSustenanceModifier",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerSustenanceModifier_Player_Source_Resource_Biome",
                schema: "game",
                table: "PlayerSustenanceModifier",
                columns: new[] { "PlayerId", "SourceType", "SourceId", "Resource", "BiomeTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerSustenanceModifier",
                schema: "game");
        }
    }
}
