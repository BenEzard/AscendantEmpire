using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS13_AddAssetBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageScope",
                schema: "assets",
                table: "Asset",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AssetBank",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    NetworkKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetBank", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetBank_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AssetBankBalance",
                schema: "assets",
                columns: table => new
                {
                    AssetBankId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetBankBalance", x => new { x.AssetBankId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_AssetBankBalance_AssetBank_AssetBankId",
                        column: x => x.AssetBankId,
                        principalSchema: "assets",
                        principalTable: "AssetBank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssetBankBalance_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                });

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
                name: "IX_AssetBankBalance_AssetId",
                schema: "assets",
                table: "AssetBankBalance",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetBankSettlementMembership_AssetBank",
                schema: "assets",
                table: "AssetBankSettlementMembership",
                column: "AssetBankId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetBankBalance",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "AssetBankSettlementMembership",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "AssetBank",
                schema: "assets");

            migrationBuilder.DropColumn(
                name: "StorageScope",
                schema: "assets",
                table: "Asset");
        }
    }
}
