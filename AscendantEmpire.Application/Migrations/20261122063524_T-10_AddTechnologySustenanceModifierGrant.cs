using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class T10_AddTechnologySustenanceModifierGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnologySustenanceModifierGrant",
                schema: "technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    Resource = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: true),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologySustenanceModifierGrant", x => x.Id);
                    table.CheckConstraint("CK_TechnologySustenanceModifierGrant_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0");
                    table.ForeignKey(
                        name: "FK_TechnologySustenanceModifierGrant_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_TechnologySustenanceModifierGrant_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnologySustenanceModifierGrant_BiomeTypeId",
                schema: "technology",
                table: "TechnologySustenanceModifierGrant",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_TechnologySustenanceModifierGrant_Technology_Resource_Biome",
                schema: "technology",
                table: "TechnologySustenanceModifierGrant",
                columns: new[] { "TechnologyId", "Resource", "BiomeTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnologySustenanceModifierGrant",
                schema: "technology");
        }
    }
}
