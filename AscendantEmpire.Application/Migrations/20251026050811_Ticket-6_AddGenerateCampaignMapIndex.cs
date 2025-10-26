using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class Ticket6_AddGenerateCampaignMapIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mapgallery");

            migrationBuilder.EnsureSchema(
                name: "util");

            migrationBuilder.CreateTable(
                name: "CampaignMapIndex",
                schema: "mapgallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreationStatus = table.Column<int>(type: "int", nullable: false),
                    CreationEngine = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CampaignMapOffsetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Numbers",
                schema: "util",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Numbers", x => x.Id);
                });

            migrationBuilder.Sql(@"
    WITH 
    E1(N) AS (SELECT 1 UNION ALL SELECT 1),
    E2(N) AS (SELECT 1 FROM E1 a, E1 b),           -- 4
    E4(N) AS (SELECT 1 FROM E2 a, E2 b),           -- 16
    E8(N) AS (SELECT 1 FROM E4 a, E4 b),           -- 256
    E16(N) AS (SELECT 1 FROM E8 a, E8 b),          -- 65,536
    E20(N) AS (SELECT 1 FROM E4 a, E16 b),         -- ~1M
    Tally AS (
        SELECT TOP (1000001) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS N
        FROM E20
    )
    INSERT INTO util.Numbers(Id)  -- if your column is Id (per your entity)
    SELECT N FROM Tally;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMapIndex",
                schema: "mapgallery");

            migrationBuilder.DropTable(
                name: "Numbers",
                schema: "util");
        }
    }
}
