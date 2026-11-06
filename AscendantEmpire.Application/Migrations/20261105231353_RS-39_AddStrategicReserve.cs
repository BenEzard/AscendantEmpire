using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS39_AddStrategicReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReservedQuantity",
                schema: "assets",
                table: "AssetBankBalance",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "PlayerStrategicReservePolicy",
                schema: "assets",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    TargetQuantity = table.Column<long>(type: "bigint", nullable: false),
                    DiversionPercent = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStrategicReservePolicy", x => new { x.PlayerId, x.AssetId });
                    table.CheckConstraint("CK_PlayerStrategicReservePolicy_DiversionPercent", "[DiversionPercent] >= 0 AND [DiversionPercent] <= 100");
                    table.ForeignKey(
                        name: "FK_PlayerStrategicReservePolicy_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerStrategicReservePolicy_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStrategicReservePolicy_Asset",
                schema: "assets",
                table: "PlayerStrategicReservePolicy",
                column: "AssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerStrategicReservePolicy",
                schema: "assets");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                schema: "assets",
                table: "AssetBankBalance");
        }
    }
}
