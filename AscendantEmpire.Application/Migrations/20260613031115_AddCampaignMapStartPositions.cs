using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignMapStartPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignMapStartPosition",
                schema: "mapgallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapGalleryIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LocationScore = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapStartPosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMapStartPosition_CampaignMapGalleryIndex_CampaignMapGalleryIndexId",
                        column: x => x.CampaignMapGalleryIndexId,
                        principalSchema: "mapgallery",
                        principalTable: "CampaignMapGalleryIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapStartPosition_Map",
                schema: "mapgallery",
                table: "CampaignMapStartPosition",
                column: "CampaignMapGalleryIndexId");

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMapStartPosition_Map_X_Y",
                schema: "mapgallery",
                table: "CampaignMapStartPosition",
                columns: new[] { "CampaignMapGalleryIndexId", "XCoord", "YCoord" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMapStartPosition",
                schema: "mapgallery");
        }
    }
}
