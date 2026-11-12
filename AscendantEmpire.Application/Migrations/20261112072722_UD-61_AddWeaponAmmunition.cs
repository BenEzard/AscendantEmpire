using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD61_AddWeaponAmmunition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitComponentWeaponAmmunition",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitComponentWeaponId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentWeaponAmmunition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponentWeaponAmmunition_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitComponentWeaponAmmunition_UnitComponentWeapon_UnitComponentWeaponId",
                        column: x => x.UnitComponentWeaponId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentWeapon",
                        principalColumn: "UnitComponentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponentWeaponAmmunition_AssetId",
                schema: "unitdesigns",
                table: "UnitComponentWeaponAmmunition",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "UX_UnitComponentWeaponAmmunition_Weapon_Asset",
                schema: "unitdesigns",
                table: "UnitComponentWeaponAmmunition",
                columns: new[] { "UnitComponentWeaponId", "AssetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitComponentWeaponAmmunition",
                schema: "unitdesigns");
        }
    }
}
