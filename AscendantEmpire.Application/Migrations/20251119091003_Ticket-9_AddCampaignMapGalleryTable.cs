using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class Ticket9_AddCampaignMapGalleryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignMapGallery",
                schema: "mapgallery",
                columns: table => new
                {
                    CampaignMapGalleryId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGalleryIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapGallery", x => x.CampaignMapGalleryId);
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignMapIndex_CampaignMapGalleryIndexId",
                        column: x => x.CampaignMapGalleryIndexId,
                        principalSchema: "mapgallery",
                        principalTable: "CampaignMapIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGalleryIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGeographyTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMapGallery",
                schema: "mapgallery");
        }
    }
}
