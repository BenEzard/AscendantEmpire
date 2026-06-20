using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddCompiledUnitTemplateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompiledUnitTemplate",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MapImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BodyComponentId = table.Column<int>(type: "int", nullable: false),
                    MovementDomain = table.Column<int>(type: "int", nullable: false),
                    TotalWeight = table.Column<long>(type: "bigint", nullable: false),
                    TotalDetection = table.Column<long>(type: "bigint", nullable: false),
                    TotalVisibility = table.Column<long>(type: "bigint", nullable: false),
                    CampaignMovementPoints = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BattlefieldMovementPoints = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MinStrength = table.Column<int>(type: "int", nullable: false),
                    MaxStrength = table.Column<int>(type: "int", nullable: false),
                    CompiledOnTurn = table.Column<int>(type: "int", nullable: false),
                    AttackBurn = table.Column<long>(type: "bigint", nullable: false),
                    AttackCrush = table.Column<long>(type: "bigint", nullable: false),
                    AttackPierce = table.Column<long>(type: "bigint", nullable: false),
                    AttackPoison = table.Column<long>(type: "bigint", nullable: false),
                    AttackSlash = table.Column<long>(type: "bigint", nullable: false),
                    ResistBurn = table.Column<long>(type: "bigint", nullable: false),
                    ResistCrush = table.Column<long>(type: "bigint", nullable: false),
                    ResistPierce = table.Column<long>(type: "bigint", nullable: false),
                    ResistPoison = table.Column<long>(type: "bigint", nullable: false),
                    ResistSlash = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledUnitTemplate", x => x.UnitTemplateId);
                    table.ForeignKey(
                        name: "FK_CompiledUnitTemplate_UnitTemplate_UnitTemplateId",
                        column: x => x.UnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompiledUnitTemplateCapability",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompiledUnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    TagCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledUnitTemplateCapability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompiledUnitTemplateCapability_CompiledUnitTemplate_CompiledUnitTemplateId",
                        column: x => x.CompiledUnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "CompiledUnitTemplate",
                        principalColumn: "UnitTemplateId");
                });

            migrationBuilder.CreateTable(
                name: "CompiledUnitTemplateComponent",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompiledUnitTemplateId = table.Column<int>(type: "int", nullable: false),
                    UnitComponentSlotId = table.Column<int>(type: "int", nullable: true),
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ComponentType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledUnitTemplateComponent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompiledUnitTemplateComponent_CompiledUnitTemplate_CompiledUnitTemplateId",
                        column: x => x.CompiledUnitTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "CompiledUnitTemplate",
                        principalColumn: "UnitTemplateId");
                });

            migrationBuilder.CreateIndex(
                name: "UX_CompiledUnitTemplateCapability_Template_Tag",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateCapability",
                columns: new[] { "CompiledUnitTemplateId", "TagCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompiledUnitTemplateComponent_Template",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent",
                column: "CompiledUnitTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompiledUnitTemplateCapability",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "CompiledUnitTemplateComponent",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "CompiledUnitTemplate",
                schema: "unitdesigns");
        }
    }
}
