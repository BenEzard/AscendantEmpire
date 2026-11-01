using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM63_AddMapResourceTypeIsWaterResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No backfill: every type in the catalogue today lives on land, so the false default is already the right answer for all of them.
            migrationBuilder.AddColumn<bool>(
                name: "IsWaterResource",
                schema: "map",
                table: "MapResourceType",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWaterResource",
                schema: "map",
                table: "MapResourceType");
        }
    }
}
