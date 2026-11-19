using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS42_AddUnitAssetBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetBank_Player_NetworkKey",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "UX_AssetBank_Settlement",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.AlterColumn<int>(
                name: "SettlementId",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Added nullable, backfilled, then tightened, rather than landing with a column default. Every bank written before the discriminator existed is a
            // settlement bank, and a default left behind on the column would quietly let a later insert omit the Kind.
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [assets].[AssetBank] SET [Kind] = 1 WHERE [Kind] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "Kind",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitInstanceId",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetBank_Player_Kind_NetworkKey",
                schema: "assets",
                table: "AssetBank",
                columns: new[] { "PlayerId", "Kind", "NetworkKey" });

            migrationBuilder.CreateIndex(
                name: "UX_AssetBank_Settlement",
                schema: "assets",
                table: "AssetBank",
                column: "SettlementId",
                unique: true,
                filter: "[SettlementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_AssetBank_UnitInstance",
                schema: "assets",
                table: "AssetBank",
                column: "UnitInstanceId",
                unique: true,
                filter: "[UnitInstanceId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssetBank_KindTargetXor",
                schema: "assets",
                table: "AssetBank",
                sql: "([Kind] = 1 AND [SettlementId] IS NOT NULL AND [UnitInstanceId] IS NULL) OR ([Kind] = 2 AND [UnitInstanceId] IS NOT NULL AND [SettlementId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetBank_UnitInstances_UnitInstanceId",
                schema: "assets",
                table: "AssetBank",
                column: "UnitInstanceId",
                principalSchema: "units",
                principalTable: "UnitInstances",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetBank_UnitInstances_UnitInstanceId",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "IX_AssetBank_Player_Kind_NetworkKey",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "UX_AssetBank_Settlement",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "UX_AssetBank_UnitInstance",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssetBank_KindTargetXor",
                schema: "assets",
                table: "AssetBank");

            // A unit bank has no Settlement to point at, so it cannot survive the column going back to NOT NULL. Rolling back therefore discards whatever
            // Forces were carrying, which is the only thing a rollback can mean here.
            migrationBuilder.Sql("""
                DELETE [Balance]
                FROM [assets].[AssetBankBalance] AS [Balance]
                INNER JOIN [assets].[AssetBank] AS [Bank] ON [Bank].[Id] = [Balance].[AssetBankId]
                WHERE [Bank].[UnitInstanceId] IS NOT NULL;

                DELETE FROM [assets].[AssetBank] WHERE [UnitInstanceId] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropColumn(
                name: "UnitInstanceId",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.AlterColumn<int>(
                name: "SettlementId",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetBank_Player_NetworkKey",
                schema: "assets",
                table: "AssetBank",
                columns: new[] { "PlayerId", "NetworkKey" });

            migrationBuilder.CreateIndex(
                name: "UX_AssetBank_Settlement",
                schema: "assets",
                table: "AssetBank",
                column: "SettlementId",
                unique: true);
        }
    }
}
