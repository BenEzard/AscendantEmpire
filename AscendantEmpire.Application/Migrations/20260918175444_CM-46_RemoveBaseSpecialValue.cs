using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Drops <c>map.CampaignBiomes.BaseSpecialValue</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Special resource was never read by any game mechanic - it only travelled through the Biome Manager, validation and map import/export - so the column is removed
    /// along with every reference to it.
    /// </para>
    /// <para>
    /// Down re-adds the column as a nullable <c>bigint</c>, but the values it held are gone: every biome comes back with a null Special value.
    /// </para>
    /// </remarks>
    public partial class CM46_RemoveBaseSpecialValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing reads this column any more, so dropping it outright is safe; its data is not preserved.
            migrationBuilder.DropColumn(
                name: "BaseSpecialValue",
                schema: "map",
                table: "CampaignBiomes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BaseSpecialValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true);
        }
    }
}
