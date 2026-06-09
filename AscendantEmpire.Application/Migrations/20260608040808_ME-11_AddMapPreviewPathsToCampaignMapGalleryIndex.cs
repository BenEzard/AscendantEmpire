using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class ME11_AddMapPreviewPathsToCampaignMapGalleryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BiomeMapPath",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeographyMapPath",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BiomeMapPath",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex");

            migrationBuilder.DropColumn(
                name: "GeographyMapPath",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex");
        }
    }
}
