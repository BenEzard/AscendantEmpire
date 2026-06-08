using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class ME10_AddBattleMapGalleryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleMapGalleryIndex",
                schema: "mapgallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapGalleryIndexId = table.Column<int>(type: "int", nullable: false),
                    CampaignTileX = table.Column<int>(type: "int", nullable: false),
                    CampaignTileY = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    GenerationSeed = table.Column<long>(type: "bigint", nullable: false),
                    GenerationStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BattleMapOffsetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMapGalleryIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BattleMapGallery",
                schema: "mapgallery",
                columns: table => new
                {
                    BattleMapGalleryId = table.Column<int>(type: "int", nullable: false),
                    BattleMapGalleryIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    AltitudeLevel = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMapGallery", x => x.BattleMapGalleryId);
                    table.ForeignKey(
                        name: "FK_BattleMapGallery_BattleMapGalleryIndex_BattleMapGalleryIndexId",
                        column: x => x.BattleMapGalleryIndexId,
                        principalSchema: "mapgallery",
                        principalTable: "BattleMapGalleryIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattleMapGallery_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleMapGallery_BattleMapGalleryIndexId_XCoord_YCoord",
                schema: "mapgallery",
                table: "BattleMapGallery",
                columns: new[] { "BattleMapGalleryIndexId", "XCoord", "YCoord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattleMapGallery_BiomeTypeId",
                schema: "mapgallery",
                table: "BattleMapGallery",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleMapGalleryIndex_CampaignMapGalleryIndexId_CampaignTileX_CampaignTileY",
                schema: "mapgallery",
                table: "BattleMapGalleryIndex",
                columns: new[] { "CampaignMapGalleryIndexId", "CampaignTileX", "CampaignTileY" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleMapGallery",
                schema: "mapgallery");

            migrationBuilder.DropTable(
                name: "BattleMapGalleryIndex",
                schema: "mapgallery");
        }
    }
}
