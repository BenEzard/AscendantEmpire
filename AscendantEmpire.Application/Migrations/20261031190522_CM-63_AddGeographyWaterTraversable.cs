using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM63_AddGeographyWaterTraversable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTraversable",
                schema: "map",
                table: "CampaignGeography",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWater",
                schema: "map",
                table: "CampaignGeography",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Both columns default to false, so only the rows that differ are backfilled. This is what makes the migration enough on its own:
            // a database that already holds Geographies is correct without re-running the seed, which would otherwise be the only writer.
            //
            // CampaignMapGeographyType: Unknown = 0, DeepWater = 1, ShallowWater = 2, Flat = 3, Hilly = 4, Mountainous = 6 (an intentional gap at 5).
            // Unknown keeps both false - it is not paintable and stands for "no data", so it must not read as open ground.
            // Mountainous keeps both false - it is the one Geography nothing can enter.
            migrationBuilder.UpdateData(
                schema: "map",
                table: "CampaignGeography",
                keyColumn: "CampaignMapGeographyTypeId",
                keyValue: 1,
                columns: ["IsWater", "IsTraversable"],
                values: [true, true]);

            migrationBuilder.UpdateData(
                schema: "map",
                table: "CampaignGeography",
                keyColumn: "CampaignMapGeographyTypeId",
                keyValue: 2,
                columns: ["IsWater", "IsTraversable"],
                values: [true, true]);

            migrationBuilder.UpdateData(
                schema: "map",
                table: "CampaignGeography",
                keyColumn: "CampaignMapGeographyTypeId",
                keyValue: 3,
                column: "IsTraversable",
                value: true);

            migrationBuilder.UpdateData(
                schema: "map",
                table: "CampaignGeography",
                keyColumn: "CampaignMapGeographyTypeId",
                keyValue: 4,
                column: "IsTraversable",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTraversable",
                schema: "map",
                table: "CampaignGeography");

            migrationBuilder.DropColumn(
                name: "IsWater",
                schema: "map",
                table: "CampaignGeography");
        }
    }
}
