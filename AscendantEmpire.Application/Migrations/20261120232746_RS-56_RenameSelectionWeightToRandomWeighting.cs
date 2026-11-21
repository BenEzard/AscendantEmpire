using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS56_RenameSelectionWeightToRandomWeighting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SelectionWeight",
                schema: "assets",
                table: "SupplySetAsset",
                newName: "RandomWeighting");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RandomWeighting",
                schema: "assets",
                table: "SupplySetAsset",
                newName: "SelectionWeight");
        }
    }
}
