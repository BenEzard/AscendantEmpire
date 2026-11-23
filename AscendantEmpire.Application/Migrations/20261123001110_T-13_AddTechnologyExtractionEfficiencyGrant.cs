using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class T13_AddTechnologyExtractionEfficiencyGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnologyExtractionEfficiencyGrant",
                schema: "technology",
                columns: table => new
                {
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyExtractionEfficiencyGrant", x => new { x.TechnologyId, x.AssetId });
                    table.CheckConstraint("CK_TechnologyExtractionEfficiencyGrant_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0");
                    table.ForeignKey(
                        name: "FK_TechnologyExtractionEfficiencyGrant_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnologyExtractionEfficiencyGrant_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyExtractionEfficiencyGrant_AssetId",
                schema: "technology",
                table: "TechnologyExtractionEfficiencyGrant",
                column: "AssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnologyExtractionEfficiencyGrant",
                schema: "technology");
        }
    }
}
