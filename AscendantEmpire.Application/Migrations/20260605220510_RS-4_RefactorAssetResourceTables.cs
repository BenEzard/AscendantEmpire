using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS4_RefactorAssetResourceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "TurnRoster",
                schema: "game");

            migrationBuilder.DropTable(
                name: "WorldResourceSnapshot",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "ResourceBank",
                schema: "assets");

            migrationBuilder.DropIndex(
                name: "IX_ResourceMetaCategory_Name",
                schema: "assets",
                table: "ResourceMetaCategory");

            migrationBuilder.DropIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_GroupOrder",
                schema: "assets",
                table: "ResourceGroup");

            migrationBuilder.DropIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_Name",
                schema: "assets",
                table: "ResourceGroup");

            migrationBuilder.DropIndex(
                name: "IX_ResourceCategory_ResourceMetaCategoryId_Name",
                schema: "assets",
                table: "ResourceCategory");

            migrationBuilder.DropIndex(
                name: "IX_Resource_Name",
                schema: "assets",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "assets",
                table: "ResourceMetaCategory");

            migrationBuilder.DropColumn(
                name: "DevNote",
                schema: "assets",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "GroupOrder",
                schema: "assets",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "assets",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "assets",
                table: "ResourceCategory");

            migrationBuilder.DropColumn(
                name: "DevNote",
                schema: "assets",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "assets",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "Properties",
                schema: "assets",
                table: "Resource");

            migrationBuilder.EnsureSchema(
                name: "resources");

            migrationBuilder.RenameTable(
                name: "ResourceMetaCategory",
                schema: "assets",
                newName: "ResourceMetaCategory",
                newSchema: "resources");

            migrationBuilder.RenameTable(
                name: "ResourceGroup",
                schema: "assets",
                newName: "ResourceGroup",
                newSchema: "resources");

            migrationBuilder.RenameTable(
                name: "ResourceCategory",
                schema: "assets",
                newName: "ResourceCategory",
                newSchema: "resources");

            migrationBuilder.RenameTable(
                name: "Resource",
                schema: "assets",
                newName: "Resource",
                newSchema: "resources");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceGroup",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceGroup",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "resources",
                table: "ResourceGroup",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceCategory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "CategoryType",
                schema: "resources",
                table: "ResourceCategory",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "resources",
                table: "Resource",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "resources",
                table: "Resource",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsGradedResource",
                schema: "resources",
                table: "Resource",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ResourceGrade",
                schema: "resources",
                table: "Resource",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetaCategory_DisplayName",
                schema: "resources",
                table: "ResourceMetaCategory",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_SortOrder",
                schema: "resources",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "SortOrder" },
                unique: true,
                filter: "[SortOrder] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCategory_ResourceMetaCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceCategory",
                columns: new[] { "ResourceMetaCategoryId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resource_DisplayName",
                schema: "resources",
                table: "Resource",
                column: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceMetaCategory_DisplayName",
                schema: "resources",
                table: "ResourceMetaCategory");

            migrationBuilder.DropIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_SortOrder",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropIndex(
                name: "IX_ResourceCategory_ResourceMetaCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceCategory");

            migrationBuilder.DropIndex(
                name: "IX_Resource_DisplayName",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceMetaCategory");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropColumn(
                name: "CategoryType",
                schema: "resources",
                table: "ResourceCategory");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "resources",
                table: "ResourceCategory");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "IsGradedResource",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ResourceGrade",
                schema: "resources",
                table: "Resource");

            migrationBuilder.EnsureSchema(
                name: "assets");

            migrationBuilder.RenameTable(
                name: "ResourceMetaCategory",
                schema: "resources",
                newName: "ResourceMetaCategory",
                newSchema: "assets");

            migrationBuilder.RenameTable(
                name: "ResourceGroup",
                schema: "resources",
                newName: "ResourceGroup",
                newSchema: "assets");

            migrationBuilder.RenameTable(
                name: "ResourceCategory",
                schema: "resources",
                newName: "ResourceCategory",
                newSchema: "assets");

            migrationBuilder.RenameTable(
                name: "Resource",
                schema: "resources",
                newName: "Resource",
                newSchema: "assets");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "assets",
                table: "ResourceMetaCategory",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "assets",
                table: "ResourceMetaCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "assets",
                table: "ResourceMetaCategory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DevNote",
                schema: "assets",
                table: "ResourceGroup",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupOrder",
                schema: "assets",
                table: "ResourceGroup",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "assets",
                table: "ResourceGroup",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "assets",
                table: "ResourceCategory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "assets",
                table: "ResourceCategory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "assets",
                table: "Resource",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "DevNote",
                schema: "assets",
                table: "Resource",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "assets",
                table: "Resource",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                schema: "assets",
                table: "Resource",
                type: "nvarchar(max)",
                nullable: true);

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
                    IsCurrentlyLuxury = table.Column<bool>(type: "bit", nullable: false),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false),
                    ObsolescenceCountdownStartedOnTurnNumber = table.Column<int>(type: "int", nullable: true),
                    UnlockedOnTurnNumber = table.Column<int>(type: "int", nullable: true)
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
                name: "TurnRoster",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    BattleId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PlayerBattleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlayerStillInBattle = table.Column<bool>(type: "bit", nullable: true),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    ScheduledTurnEndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TurnEndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TurnOrder = table.Column<int>(type: "int", nullable: false),
                    TurnStartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnRoster", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnRoster_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TurnRoster_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldResourceSnapshot",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceDefinitionId = table.Column<int>(type: "int", nullable: false),
                    RelativeValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalWorldSupply = table.Column<long>(type: "bigint", nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetaCategory_Name",
                schema: "assets",
                table: "ResourceMetaCategory",
                column: "Name",
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
                name: "IX_ResourceCategory_ResourceMetaCategoryId_Name",
                schema: "assets",
                table: "ResourceCategory",
                columns: new[] { "ResourceMetaCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resource_Name",
                schema: "assets",
                table: "Resource",
                column: "Name");

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
                name: "IX_TurnRoster_PlayerId",
                schema: "game",
                table: "TurnRoster",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_TurnRoster_Battle_Game_Battle_Player_Round",
                schema: "game",
                table: "TurnRoster",
                columns: new[] { "GameId", "BattleId", "PlayerId", "RoundNumber" },
                unique: true,
                filter: "[BattleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TurnRoster_Campaign_Game_Player_Round",
                schema: "game",
                table: "TurnRoster",
                columns: new[] { "GameId", "PlayerId", "RoundNumber" },
                unique: true,
                filter: "[BattleId] IS NULL");

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
        }
    }
}
