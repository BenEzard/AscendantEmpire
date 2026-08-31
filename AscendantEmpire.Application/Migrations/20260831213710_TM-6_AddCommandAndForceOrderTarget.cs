using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class TM6_AddCommandAndForceOrderTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandId",
                schema: "units",
                table: "UnitInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UnitInstanceId",
                schema: "units",
                table: "ForceOrders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CommandId",
                schema: "units",
                table: "ForceOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Commands",
                schema: "units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    OwnerPlayerId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    BattleMapTileId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Commands_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_Commands_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Commands_Player_OwnerPlayerId",
                        column: x => x.OwnerPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitInstances_CommandId",
                schema: "units",
                table: "UnitInstances",
                column: "CommandId",
                filter: "[CommandId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ForceOrders_CommandId",
                schema: "units",
                table: "ForceOrders",
                column: "CommandId",
                filter: "[CommandId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ForceOrders_CommandId_OrderStatus",
                schema: "units",
                table: "ForceOrders",
                columns: new[] { "CommandId", "OrderStatus" },
                filter: "[CommandId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ForceOrders_UnitOrCommandXor",
                schema: "units",
                table: "ForceOrders",
                sql: "([UnitInstanceId] IS NOT NULL AND [CommandId] IS NULL) OR ([UnitInstanceId] IS NULL AND [CommandId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_CampaignMapTileId",
                schema: "units",
                table: "Commands",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_GameId",
                schema: "units",
                table: "Commands",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_OwnerPlayerId",
                schema: "units",
                table: "Commands",
                column: "OwnerPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ForceOrders_Commands_CommandId",
                schema: "units",
                table: "ForceOrders",
                column: "CommandId",
                principalSchema: "units",
                principalTable: "Commands",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UnitInstances_Commands_CommandId",
                schema: "units",
                table: "UnitInstances",
                column: "CommandId",
                principalSchema: "units",
                principalTable: "Commands",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForceOrders_Commands_CommandId",
                schema: "units",
                table: "ForceOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitInstances_Commands_CommandId",
                schema: "units",
                table: "UnitInstances");

            migrationBuilder.DropTable(
                name: "Commands",
                schema: "units");

            migrationBuilder.DropIndex(
                name: "IX_UnitInstances_CommandId",
                schema: "units",
                table: "UnitInstances");

            migrationBuilder.DropIndex(
                name: "IX_ForceOrders_CommandId",
                schema: "units",
                table: "ForceOrders");

            migrationBuilder.DropIndex(
                name: "IX_ForceOrders_CommandId_OrderStatus",
                schema: "units",
                table: "ForceOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ForceOrders_UnitOrCommandXor",
                schema: "units",
                table: "ForceOrders");

            migrationBuilder.DropColumn(
                name: "CommandId",
                schema: "units",
                table: "UnitInstances");

            migrationBuilder.DropColumn(
                name: "CommandId",
                schema: "units",
                table: "ForceOrders");

            migrationBuilder.AlterColumn<int>(
                name: "UnitInstanceId",
                schema: "units",
                table: "ForceOrders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
