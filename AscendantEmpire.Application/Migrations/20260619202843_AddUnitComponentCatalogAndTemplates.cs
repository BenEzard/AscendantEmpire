using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitComponentCatalogAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tag",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitComponent",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComponentType = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnlockedComponent",
                schema: "unitdesigns",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnTurn = table.Column<int>(type: "int", nullable: false),
                    ObsoleteOnTurn = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedComponent", x => new { x.PlayerId, x.UnitComponentId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedComponent_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedComponent_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentBody",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    MovementDomain = table.Column<int>(type: "int", nullable: false),
                    CampaignMovementPoints = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BattlefieldMovementPoints = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MinStrength = table.Column<int>(type: "int", nullable: false),
                    MaxStrength = table.Column<int>(type: "int", nullable: false),
                    IncrementStrength = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentBody", x => x.UnitComponentId);
                    table.ForeignKey(
                        name: "FK_UnitComponentBody_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentTag",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentTag", x => new { x.UnitComponentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_UnitComponentTag_Tag_TagId",
                        column: x => x.TagId,
                        principalSchema: "unitdesigns",
                        principalTable: "Tag",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitComponentTag_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitTemplate",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    BodyComponentId = table.Column<int>(type: "int", nullable: false),
                    MapImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTemplate_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitTemplate_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitTemplate_UnitComponent_BodyComponentId",
                        column: x => x.BodyComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentSlot",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitComponentBodyId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    UIXPosition = table.Column<int>(type: "int", nullable: false),
                    UIYPosition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentSlot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponentSlot_UnitComponentBody_UnitComponentBodyId",
                        column: x => x.UnitComponentBodyId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentBody",
                        principalColumn: "UnitComponentId");
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnlockedTemplate",
                schema: "unitdesigns",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnTurn = table.Column<int>(type: "int", nullable: false),
                    ObsoleteOnTurn = table.Column<int>(type: "int", nullable: true),
                    HasBeenPrototyped = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedTemplate", x => new { x.PlayerId, x.UnitTemplateId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedTemplate_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedTemplate_UnitTemplate_UnitTemplateId",
                        column: x => x.UnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentSlotRule",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitComponentSlotId = table.Column<int>(type: "int", nullable: false),
                    RuleType = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentSlotRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponentSlotRule_UnitComponentSlot_UnitComponentSlotId",
                        column: x => x.UnitComponentSlotId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentSlot",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitTemplateComponent",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    UnitComponentSlotId = table.Column<int>(type: "int", nullable: false),
                    UnitComponentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTemplateComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId",
                        column: x => x.UnitComponentSlotId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentSlot",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitTemplateComponent_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitTemplateComponent_UnitTemplate_UnitTemplateId",
                        column: x => x.UnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedComponent_UnitComponent",
                schema: "unitdesigns",
                table: "PlayerUnlockedComponent",
                column: "UnitComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedTemplate_UnitTemplate",
                schema: "unitdesigns",
                table: "PlayerUnlockedTemplate",
                column: "UnitTemplateId");

            migrationBuilder.CreateIndex(
                name: "UX_Tag_Code",
                schema: "unitdesigns",
                table: "Tag",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponent_ComponentType",
                schema: "unitdesigns",
                table: "UnitComponent",
                column: "ComponentType");

            migrationBuilder.CreateIndex(
                name: "UX_UnitComponent_Code",
                schema: "unitdesigns",
                table: "UnitComponent",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponentSlot_Body",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                column: "UnitComponentBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponentSlotRule_Slot",
                schema: "unitdesigns",
                table: "UnitComponentSlotRule",
                column: "UnitComponentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponentTag_Tag",
                schema: "unitdesigns",
                table: "UnitComponentTag",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplate_BodyComponent",
                schema: "unitdesigns",
                table: "UnitTemplate",
                column: "BodyComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplate_GameId",
                schema: "unitdesigns",
                table: "UnitTemplate",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplate_PlayerId",
                schema: "unitdesigns",
                table: "UnitTemplate",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_UnitTemplate_Code",
                schema: "unitdesigns",
                table: "UnitTemplate",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplateComponent_Component",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                column: "UnitComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplateComponent_Slot",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                column: "UnitComponentSlotId");

            migrationBuilder.CreateIndex(
                name: "UX_UnitTemplateComponent_Template_Slot",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                columns: new[] { "UnitTemplateId", "UnitComponentSlotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerUnlockedComponent",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "PlayerUnlockedTemplate",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentSlotRule",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentTag",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitTemplateComponent",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "Tag",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentSlot",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitTemplate",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentBody",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponent",
                schema: "unitdesigns");
        }
    }
}
