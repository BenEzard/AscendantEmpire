using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class ScaleBiomeBaseValuesToBigint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "BaseWoodValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseStoneValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseSpecialValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseHuntValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseForageValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseFodderValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseFishValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "BaseBoneValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Backfill existing rows, which still hold un-scaled whole values, into the scaled storage
            // representation (whole * 10,000). The columns are now bigint so there is no overflow risk.
            // NULL values (resource not applicable) are left as NULL by the multiplication.
            migrationBuilder.Sql(
                """
                UPDATE map.CampaignBiomes
                SET
                    BaseForageValue  = BaseForageValue  * 10000,
                    BaseHuntValue    = BaseHuntValue    * 10000,
                    BaseFishValue    = BaseFishValue    * 10000,
                    BaseFodderValue  = BaseFodderValue  * 10000,
                    BaseBoneValue    = BaseBoneValue    * 10000,
                    BaseWoodValue    = BaseWoodValue    * 10000,
                    BaseStoneValue   = BaseStoneValue   * 10000,
                    BaseSpecialValue = BaseSpecialValue * 10000;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Un-scale existing rows back to whole values BEFORE narrowing to int, so any large scaled
            // values are divided down while the columns are still bigint and cannot be truncated.
            migrationBuilder.Sql(
                """
                UPDATE map.CampaignBiomes
                SET
                    BaseForageValue  = BaseForageValue  / 10000,
                    BaseHuntValue    = BaseHuntValue    / 10000,
                    BaseFishValue    = BaseFishValue    / 10000,
                    BaseFodderValue  = BaseFodderValue  / 10000,
                    BaseBoneValue    = BaseBoneValue    / 10000,
                    BaseWoodValue    = BaseWoodValue    / 10000,
                    BaseStoneValue   = BaseStoneValue   / 10000,
                    BaseSpecialValue = BaseSpecialValue / 10000;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "BaseWoodValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseStoneValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseSpecialValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseHuntValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseForageValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseFodderValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseFishValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BaseBoneValue",
                schema: "map",
                table: "CampaignBiomes",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
