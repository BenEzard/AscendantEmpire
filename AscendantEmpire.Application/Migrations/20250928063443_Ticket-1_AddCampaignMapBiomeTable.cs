using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class Ticket1_AddCampaignMapBiomeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "map");

            migrationBuilder.CreateTable(
                name: "CampaignBiomes",
                schema: "map",
                columns: table => new
                {
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DoesBlockSight = table.Column<bool>(type: "bit", nullable: false),
                    IsWater = table.Column<bool>(type: "bit", nullable: false),
                    IsTraversable = table.Column<bool>(type: "bit", nullable: false),
                    RegenerationRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    IsPaintableOnCampaignMap = table.Column<bool>(type: "bit", nullable: false),
                    MapEditorHtmlColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CanBeElevated = table.Column<bool>(type: "bit", nullable: false),
                    BaseForageValue = table.Column<int>(type: "int", nullable: true),
                    BaseHuntValue = table.Column<int>(type: "int", nullable: true),
                    BaseFishValue = table.Column<int>(type: "int", nullable: true),
                    BaseFodderValue = table.Column<int>(type: "int", nullable: true),
                    BaseBoneValue = table.Column<int>(type: "int", nullable: true),
                    BaseWoodValue = table.Column<int>(type: "int", nullable: true),
                    BaseStoneValue = table.Column<int>(type: "int", nullable: true),
                    BaseSpecialValue = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignBiomes", x => x.BiomeTypeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignBiomes",
                schema: "map");
        }
    }
}
