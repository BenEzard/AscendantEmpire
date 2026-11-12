using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD60_AddSupplySets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplySet",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplyType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplySet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplySetAsset",
                schema: "assets",
                columns: table => new
                {
                    SupplySetId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    AmountPerUnit = table.Column<long>(type: "bigint", nullable: false),
                    SelectionWeight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplySetAsset", x => new { x.SupplySetId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_SupplySetAsset_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplySetAsset_SupplySet_SupplySetId",
                        column: x => x.SupplySetId,
                        principalSchema: "assets",
                        principalTable: "SupplySet",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitComponentSupplyRequirement",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitComponentId = table.Column<int>(type: "int", nullable: false),
                    SupplySetId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponentSupplyRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponentSupplyRequirement_SupplySet_SupplySetId",
                        column: x => x.SupplySetId,
                        principalSchema: "assets",
                        principalTable: "SupplySet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitComponentSupplyRequirement_UnitComponent_UnitComponentId",
                        column: x => x.UnitComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_SupplySet_Code",
                schema: "assets",
                table: "SupplySet",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplySetAsset_Asset",
                schema: "assets",
                table: "SupplySetAsset",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponentSupplyRequirement_SupplySet",
                schema: "unitdesigns",
                table: "UnitComponentSupplyRequirement",
                column: "SupplySetId");

            migrationBuilder.CreateIndex(
                name: "UX_UnitComponentSupplyRequirement_Component_SupplySet",
                schema: "unitdesigns",
                table: "UnitComponentSupplyRequirement",
                columns: new[] { "UnitComponentId", "SupplySetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplySetAsset",
                schema: "assets");

            migrationBuilder.DropTable(
                name: "UnitComponentSupplyRequirement",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "SupplySet",
                schema: "assets");
        }
    }
}
