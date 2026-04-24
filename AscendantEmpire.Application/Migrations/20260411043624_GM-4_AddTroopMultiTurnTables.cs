using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM4_AddTroopMultiTurnTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "troops");

            migrationBuilder.CreateTable(
                name: "TroopOrders",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    TroopInstanceId = table.Column<int>(type: "int", nullable: false),
                    OrderType = table.Column<int>(type: "int", nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    TargetCampaignMapTileId = table.Column<int>(type: "int", nullable: true),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    ResourceDefinitionId = table.Column<int>(type: "int", nullable: true),
                    CurrentMovementStepIndex = table.Column<int>(type: "int", nullable: true),
                    WorkRequired = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WorkCompleted = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    EffectiveFromTurnNumber = table.Column<int>(type: "int", nullable: true),
                    TradeSourceStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    TradeTargetStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    DefendTargetTroopInstanceId = table.Column<int>(type: "int", nullable: true),
                    DefendTargetStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    CreatedTurnNumber = table.Column<int>(type: "int", nullable: false),
                    LastProcessedTurnNumber = table.Column<int>(type: "int", nullable: true),
                    CompletedTurnNumber = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrders_TroopInstances_TroopInstanceId",
                        column: x => x.TroopInstanceId,
                        principalSchema: "units",
                        principalTable: "TroopInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopOrderMovementSteps",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TroopOrderId = table.Column<int>(type: "int", nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CompletedTurnNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrderMovementSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrderMovementSteps_TroopOrders_TroopOrderId",
                        column: x => x.TroopOrderId,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopOrderParticipants",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TroopOrderId = table.Column<int>(type: "int", nullable: false),
                    TroopInstanceId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    TroopOrderId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrderParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopInstances_TroopInstanceId",
                        column: x => x.TroopInstanceId,
                        principalSchema: "units",
                        principalTable: "TroopInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopOrders_TroopOrderId",
                        column: x => x.TroopOrderId,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopOrders_TroopOrderId1",
                        column: x => x.TroopOrderId1,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_CampaignMapTileId_IsCompleted",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                columns: new[] { "CampaignMapTileId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_TroopOrderId",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                column: "TroopOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_TroopOrderId_StepIndex",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                columns: new[] { "TroopOrderId", "StepIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                columns: new[] { "TroopOrderId", "TroopInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId1",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_DefendTargetStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "DefendTargetStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_DefendTargetTroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "DefendTargetTroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId",
                schema: "troops",
                table: "TroopOrders",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_OrderType_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "OrderType", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "TroopInstanceId", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_ResourceDefinitionId",
                schema: "troops",
                table: "TroopOrders",
                column: "ResourceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_StructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "StructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TargetCampaignMapTileId",
                schema: "troops",
                table: "TroopOrders",
                column: "TargetCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TradeSourceStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TradeSourceStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TradeTargetStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TradeTargetStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "TroopInstanceId", "OrderStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TroopOrderMovementSteps",
                schema: "troops");

            migrationBuilder.DropTable(
                name: "TroopOrderParticipants",
                schema: "troops");

            migrationBuilder.DropTable(
                name: "TroopOrders",
                schema: "troops");
        }
    }
}
