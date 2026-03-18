using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS2_AddAssetBankTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerTroopSummaries_GameIndex_GameId",
                schema: "game",
                table: "PlayerTroopSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerTroopSummaries_Player_PlayerId",
                schema: "game",
                table: "PlayerTroopSummaries");

            migrationBuilder.EnsureSchema(
                name: "assets");

            migrationBuilder.CreateTable(
                name: "ResourceBank",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FactionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    NetworkKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceMetaCategory",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMetaCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceBankSettlementMembership",
                schema: "assets",
                columns: table => new
                {
                    SettlementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceBankId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceBankSettlementMembership", x => x.SettlementId);
                    table.ForeignKey(
                        name: "FK_ResourceBankSettlementMembership_ResourceBank_ResourceBankId",
                        column: x => x.ResourceBankId,
                        principalSchema: "assets",
                        principalTable: "ResourceBank",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceCategory",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceMetaCategoryId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCategory_ResourceMetaCategory_ResourceMetaCategoryId",
                        column: x => x.ResourceMetaCategoryId,
                        principalSchema: "assets",
                        principalTable: "ResourceMetaCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceGroup",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceCategoryId = table.Column<int>(type: "int", nullable: false),
                    GroupOrder = table.Column<int>(type: "int", nullable: false),
                    DevNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceGroup_ResourceCategory_ResourceCategoryId",
                        column: x => x.ResourceCategoryId,
                        principalSchema: "assets",
                        principalTable: "ResourceCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resource",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResourceMetaCategoryId = table.Column<int>(type: "int", nullable: false),
                    ResourceGroupId = table.Column<int>(type: "int", nullable: false),
                    NeverLuxury = table.Column<bool>(type: "bit", nullable: false),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DevNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resource_ResourceGroup_ResourceGroupId",
                        column: x => x.ResourceGroupId,
                        principalSchema: "assets",
                        principalTable: "ResourceGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Resource_ResourceMetaCategory_ResourceMetaCategoryId",
                        column: x => x.ResourceMetaCategoryId,
                        principalSchema: "assets",
                        principalTable: "ResourceMetaCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactionResourcePriority",
                schema: "assets",
                columns: table => new
                {
                    FactionId = table.Column<int>(type: "int", nullable: false),
                    ResourceCategoryId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    PriorityOrder = table.Column<int>(type: "int", nullable: false),
                    ResourceGroupId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionResourcePriority", x => new { x.FactionId, x.ResourceCategoryId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_FactionResourcePriority_ResourceCategory_ResourceCategoryId",
                        column: x => x.ResourceCategoryId,
                        principalSchema: "assets",
                        principalTable: "ResourceCategory",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FactionResourcePriority_ResourceGroup_ResourceGroupId",
                        column: x => x.ResourceGroupId,
                        principalSchema: "assets",
                        principalTable: "ResourceGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FactionResourcePriority_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "assets",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FactionResourceState",
                schema: "assets",
                columns: table => new
                {
                    FactionId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    AvailabilityState = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnTurnNumber = table.Column<int>(type: "int", nullable: true),
                    ObsolescenceCountdownStartedOnTurnNumber = table.Column<int>(type: "int", nullable: true),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false),
                    IsCurrentlyLuxury = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionResourceState", x => new { x.FactionId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_FactionResourceState_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "assets",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceBankBalance",
                schema: "assets",
                columns: table => new
                {
                    ResourceBankId = table.Column<int>(type: "int", nullable: false),
                    ResourceDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceBankBalance", x => new { x.ResourceBankId, x.ResourceDefinitionId });
                    table.ForeignKey(
                        name: "FK_ResourceBankBalance_ResourceBank_ResourceBankId",
                        column: x => x.ResourceBankId,
                        principalSchema: "assets",
                        principalTable: "ResourceBank",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourceBankBalance_Resource_ResourceDefinitionId",
                        column: x => x.ResourceDefinitionId,
                        principalSchema: "assets",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorldResourceSnapshot",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    ResourceDefinitionId = table.Column<int>(type: "int", nullable: false),
                    TotalWorldSupply = table.Column<long>(type: "bigint", nullable: false),
                    RelativeValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldResourceSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldResourceSnapshot_Resource_ResourceDefinitionId",
                        column: x => x.ResourceDefinitionId,
                        principalSchema: "assets",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourcePriority_FactionId_ResourceCategoryId_PriorityOrder",
                schema: "assets",
                table: "FactionResourcePriority",
                columns: new[] { "FactionId", "ResourceCategoryId", "PriorityOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourcePriority_ResourceCategoryId",
                schema: "assets",
                table: "FactionResourcePriority",
                column: "ResourceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourcePriority_ResourceGroupId",
                schema: "assets",
                table: "FactionResourcePriority",
                column: "ResourceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourcePriority_ResourceId",
                schema: "assets",
                table: "FactionResourcePriority",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourceState_FactionId_AvailabilityState",
                schema: "assets",
                table: "FactionResourceState",
                columns: new[] { "FactionId", "AvailabilityState" });

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourceState_FactionId_IsCurrentlyLuxury",
                schema: "assets",
                table: "FactionResourceState",
                columns: new[] { "FactionId", "IsCurrentlyLuxury" });

            migrationBuilder.CreateIndex(
                name: "IX_FactionResourceState_ResourceId",
                schema: "assets",
                table: "FactionResourceState",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_Code",
                schema: "assets",
                table: "Resource",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resource_Name",
                schema: "assets",
                table: "Resource",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceGroupId",
                schema: "assets",
                table: "Resource",
                column: "ResourceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceMetaCategoryId",
                schema: "assets",
                table: "Resource",
                column: "ResourceMetaCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBank_FactionId_Kind",
                schema: "assets",
                table: "ResourceBank",
                columns: new[] { "FactionId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBank_NetworkKey",
                schema: "assets",
                table: "ResourceBank",
                column: "NetworkKey");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBankBalance_ResourceDefinitionId",
                schema: "assets",
                table: "ResourceBankBalance",
                column: "ResourceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBankSettlementMembership_ResourceBankId",
                schema: "assets",
                table: "ResourceBankSettlementMembership",
                column: "ResourceBankId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCategory_ResourceMetaCategoryId_Name",
                schema: "assets",
                table: "ResourceCategory",
                columns: new[] { "ResourceMetaCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_GroupOrder",
                schema: "assets",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "GroupOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_Name",
                schema: "assets",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetaCategory_Name",
                schema: "assets",
                table: "ResourceMetaCategory",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldResourceSnapshot_ResourceDefinitionId",
                schema: "assets",
                table: "WorldResourceSnapshot",
                column: "ResourceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldResourceSnapshot_TurnNumber_ResourceDefinitionId",
                schema: "assets",
                table: "WorldResourceSnapshot",
                columns: new[] { "TurnNumber", "ResourceDefinitionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerTroopSummaries_GameIndex_GameId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerTroopSummaries_Player_PlayerId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerTroopSummaries_GameIndex_GameId",
                schema: "game",
                table: "PlayerTroopSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerTroopSummaries_Player_PlayerId",
                schema: "game",
                table: "PlayerTroopSummaries");

            migrationBuilder.DropTable(
                name: "FactionResourcePriority",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "FactionResourceState",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceBankBalance",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceBankSettlementMembership",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "WorldResourceSnapshot",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceBank",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "Resource",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceGroup",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceCategory",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceMetaCategory",
                schema: "assets");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerTroopSummaries_GameIndex_GameId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerTroopSummaries_Player_PlayerId",
                schema: "game",
                table: "PlayerTroopSummaries",
                column: "PlayerId",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id");
        }
    }
}
