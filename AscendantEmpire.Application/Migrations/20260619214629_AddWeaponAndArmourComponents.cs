using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddWeaponAndArmourComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CrushAttack",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UnitComponentArmour",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    ResistBurn = table.Column<long>(type: "bigint", nullable: false),
                    ResistCrush = table.Column<long>(type: "bigint", nullable: false),
                    ResistPierce = table.Column<long>(type: "bigint", nullable: false),
                    ResistPoison = table.Column<long>(type: "bigint", nullable: false),
                    ResistSlash = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentArmour", x => x.UnitComponentId);
                    table.ForeignKey(
                        name: "FK_UnitComponentArmour_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentWeapon",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    MinimumRange = table.Column<int>(type: "int", nullable: false),
                    MaximumRange = table.Column<int>(type: "int", nullable: false),
                    Burn = table.Column<long>(type: "bigint", nullable: false),
                    Crush = table.Column<long>(type: "bigint", nullable: false),
                    Pierce = table.Column<long>(type: "bigint", nullable: false),
                    Poison = table.Column<long>(type: "bigint", nullable: false),
                    Slash = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentWeapon", x => x.UnitComponentId);
                    table.ForeignKey(
                        name: "FK_UnitComponentWeapon_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentWeaponDomainModifier",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitComponentWeaponId = table.Column<int>(type: "int", nullable: false),
                    Domain = table.Column<int>(type: "int", nullable: false),
                    DamageMultiplier = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentWeaponDomainModifier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponentWeaponDomainModifier_UnitComponentWeapon_UnitComponentWeaponId",
                        column: x => x.UnitComponentWeaponId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentWeapon",
                        principalColumn: "UnitComponentId");
                });

            migrationBuilder.CreateIndex(
                name: "UX_UnitComponentWeaponDomainModifier_Weapon_Domain",
                schema: "unitdesigns",
                table: "UnitComponentWeaponDomainModifier",
                columns: new[] { "UnitComponentWeaponId", "Domain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitComponentArmour",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentWeaponDomainModifier",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitComponentWeapon",
                schema: "unitdesigns");

            migrationBuilder.DropColumn(
                name: "CrushAttack",
                schema: "unitdesigns",
                table: "UnitComponentBody");
        }
    }
}
