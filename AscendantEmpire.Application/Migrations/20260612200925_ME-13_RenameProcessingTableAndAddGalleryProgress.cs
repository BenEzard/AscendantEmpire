using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class ME13_RenameProcessingTableAndAddGalleryProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Processing",
                schema: "system",
                table: "Processing");

            migrationBuilder.RenameTable(
                name: "Processing",
                schema: "system",
                newName: "BackgroundProcessing",
                newSchema: "system");

            migrationBuilder.AddColumn<byte>(
                name: "ProgressPercent",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BackgroundProcessing",
                schema: "system",
                table: "BackgroundProcessing",
                column: "ProcessingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BackgroundProcessing",
                schema: "system",
                table: "BackgroundProcessing");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                schema: "mapgallery",
                table: "CampaignMapGalleryIndex");

            migrationBuilder.RenameTable(
                name: "BackgroundProcessing",
                schema: "system",
                newName: "Processing",
                newSchema: "system");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Processing",
                schema: "system",
                table: "Processing",
                column: "ProcessingId");
        }
    }
}
