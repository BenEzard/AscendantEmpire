using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RescaleBiomeBaseValuesTo1000 : Migration
    {
        // The previous migration (ScaleBiomeBaseValuesToBigint) backfilled the Base*Value columns to a
        // scale of 10,000. ScaledAmount.Scale is now 1,000, so existing rows must be divided by 10 to match.
        // The stored values are whole * 10,000 (exact multiples of 10,000), so the integer division is lossless.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE map.CampaignBiomes
                SET
                    BaseForageValue  = BaseForageValue  / 10,
                    BaseHuntValue    = BaseHuntValue    / 10,
                    BaseFishValue    = BaseFishValue    / 10,
                    BaseFodderValue  = BaseFodderValue  / 10,
                    BaseBoneValue    = BaseBoneValue    / 10,
                    BaseWoodValue    = BaseWoodValue    / 10,
                    BaseStoneValue   = BaseStoneValue   / 10,
                    BaseSpecialValue = BaseSpecialValue / 10;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the 10,000 scale used before this migration.
            migrationBuilder.Sql(
                """
                UPDATE map.CampaignBiomes
                SET
                    BaseForageValue  = BaseForageValue  * 10,
                    BaseHuntValue    = BaseHuntValue    * 10,
                    BaseFishValue    = BaseFishValue    * 10,
                    BaseFodderValue  = BaseFodderValue  * 10,
                    BaseBoneValue    = BaseBoneValue    * 10,
                    BaseWoodValue    = BaseWoodValue    * 10,
                    BaseStoneValue   = BaseStoneValue   * 10,
                    BaseSpecialValue = BaseSpecialValue * 10;
                """);
        }
    }
}
