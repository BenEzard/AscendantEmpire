using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Renames <c>map.CampaignBiomes.RegenerationRate</c> to <c>RegenerationPercent</c> and converts every biome's value from a multiplier to a fraction of Base.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regeneration changed from multiplying what is left on a tile (1.25) to regrowing a fixed share of Base every Round (0.25). The new values are the old multipliers less 1,
    /// matching <c>BiomeSeedData</c>. Unknown and Void have no sustenance, so their value is null.
    /// </para>
    /// <para>
    /// The column keeps its <c>decimal(18,3)</c> type, so it is renamed rather than dropped and re-added. A biome edited away from the seeded values in the Biome Manager is
    /// overwritten with the seeded fraction, since an arbitrary old multiplier cannot be trusted to convert sensibly.
    /// </para>
    /// </remarks>
    public partial class CM43_RenameRegenerationRateToRegenerationPercent : Migration
    {
        /// <summary>
        /// Each biome, by <c>BiomeTypeId</c>, with its Regeneration Percent (new) and Regeneration Rate (old).
        /// </summary>
        private static readonly (int BiomeTypeId, decimal? RegenerationPercent, decimal? RegenerationRate)[] _BIOME_VALUES =
        [
            (0, null, null), // Unknown
            (1, 0.25m, 1.25m), // Coastal
            (2, 0.25m, 1.25m), // Ocean
            (3, 0.1m, 1.1m), // Desert
            (4, 0.15m, 1.15m), // Arid
            (5, 0.05m, 1.05m), // Ice
            (6, 0.15m, 1.15m), // Tundra
            (7, 0.25m, 1.25m), // Grassland
            (8, 0.3m, 1.3m), // Forest
            (9, 0.5m, 1.5m), // Jungle
            (10, 0.25m, 1.25m), // Woodland
            (11, null, null), // Void
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RegenerationRate",
                schema: "map",
                table: "CampaignBiomes",
                newName: "RegenerationPercent");

            // A missing biome row simply updates nothing, so this is safe on a database that has not been seeded.
            foreach ((int biomeTypeId, decimal? regenerationPercent, decimal? _) in _BIOME_VALUES)
            {
                migrationBuilder.UpdateData(
                    schema: "map",
                    table: "CampaignBiomes",
                    keyColumn: "BiomeTypeId",
                    keyValue: biomeTypeId,
                    column: "RegenerationPercent",
                    value: regenerationPercent);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the old multipliers before the column takes its old name again.
            foreach ((int biomeTypeId, decimal? _, decimal? regenerationRate) in _BIOME_VALUES)
            {
                migrationBuilder.UpdateData(
                    schema: "map",
                    table: "CampaignBiomes",
                    keyColumn: "BiomeTypeId",
                    keyValue: biomeTypeId,
                    column: "RegenerationPercent",
                    value: regenerationRate);
            }

            migrationBuilder.RenameColumn(
                name: "RegenerationPercent",
                schema: "map",
                table: "CampaignBiomes",
                newName: "RegenerationRate");
        }
    }
}
