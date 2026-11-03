using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS33_AssetBankPerSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetBankSettlementMembership",
                schema: "assets");

            migrationBuilder.DropIndex(
                name: "IX_AssetBank_Player",
                schema: "assets",
                table: "AssetBank");

            // No defaultValue: a bank with no settlement is not a thing, so the column has no sensible default and should not carry a DEFAULT constraint
            // that lets one be inserted. It also means this refuses outright against a non-empty AssetBank table, which is the wanted behaviour - the rows
            // are cleared before this runs, because there is no honest value to backfill.
            migrationBuilder.AddColumn<int>(
                name: "SettlementId",
                schema: "assets",
                table: "AssetBank",
                type: "int",
                nullable: false);

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

            migrationBuilder.AddForeignKey(
                name: "FK_AssetBank_Settlements_SettlementId",
                schema: "assets",
                table: "AssetBank",
                column: "SettlementId",
                principalSchema: "settlement",
                principalTable: "Settlements",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetBank_Settlements_SettlementId",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "IX_AssetBank_Player_NetworkKey",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropIndex(
                name: "UX_AssetBank_Settlement",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                schema: "assets",
                table: "AssetBank");

            migrationBuilder.CreateTable(
                name: "AssetBankSettlementMembership",
                schema: "assets",
                columns: table => new
                {
                    SettlementId = table.Column<int>(type: "int", nullable: false),
                    AssetBankId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetBankSettlementMembership", x => x.SettlementId);
                    table.ForeignKey(
                        name: "FK_AssetBankSettlementMembership_AssetBank_AssetBankId",
                        column: x => x.AssetBankId,
                        principalSchema: "assets",
                        principalTable: "AssetBank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetBankSettlementMembership_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "settlement",
                        principalTable: "Settlements",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetBank_Player",
                schema: "assets",
                table: "AssetBank",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetBankSettlementMembership_AssetBank",
                schema: "assets",
                table: "AssetBankSettlementMembership",
                column: "AssetBankId");
        }
    }
}
