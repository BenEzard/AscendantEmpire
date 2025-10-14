using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class Ticket4_AddCampaignGeographyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignGeography",
                schema: "map",
                columns: table => new
                {
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelativeElevation = table.Column<int>(type: "int", nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MapEditorIcon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPaintableOnCampaignMap = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignGeography", x => x.CampaignMapGeographyTypeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignGeography",
                schema: "map");
        }
    }
}
