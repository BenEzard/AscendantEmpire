using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Adds river edges to the campaign map: a <c>SouthRiver</c> and <c>EastRiver</c> width on every gallery tile and every game tile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each tile owns only its South and East edges, so every edge between two tiles is stored exactly once. Both are <c>tinyint</c>, because these four
    /// columns sit on every tile of every map and every game; see <c>RiverWidth</c>.
    /// </para>
    /// <para>
    /// They default to 0 (<c>RiverWidth.None</c>) through named constraints, so existing maps and games read as river-less, and the raw-SQL and bulk-copy
    /// tile writers that don't name the columns still insert.
    /// </para>
    /// </remarks>
    public partial class CM52_AddCampaignMapRivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "EastRiver",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0)
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMapGallery_EastRiver");

            migrationBuilder.AddColumn<byte>(
                name: "SouthRiver",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0)
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMapGallery_SouthRiver");

            migrationBuilder.AddColumn<byte>(
                name: "EastRiver",
                schema: "game",
                table: "CampaignMap",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0)
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_EastRiver");

            migrationBuilder.AddColumn<byte>(
                name: "SouthRiver",
                schema: "game",
                table: "CampaignMap",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0)
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_SouthRiver");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EastRiver",
                schema: "mapgallery",
                table: "CampaignMapGallery")
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMapGallery_EastRiver");

            migrationBuilder.DropColumn(
                name: "SouthRiver",
                schema: "mapgallery",
                table: "CampaignMapGallery")
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMapGallery_SouthRiver");

            migrationBuilder.DropColumn(
                name: "EastRiver",
                schema: "game",
                table: "CampaignMap")
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_EastRiver");

            migrationBuilder.DropColumn(
                name: "SouthRiver",
                schema: "game",
                table: "CampaignMap")
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_SouthRiver");
        }
    }
}
