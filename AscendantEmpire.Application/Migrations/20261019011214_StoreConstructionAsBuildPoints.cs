using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class StoreConstructionAsBuildPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The catalogue's total is renamed, so the authored costs survive. A Structure's own progress is dropped and re-added rather than converted: it changes from a
            // percentage to raw build points, and there is no honest conversion without the kind's total per row. Anything part-built restarts at 0; anything finished
            // stays finished, because CompletedOnTurnId is what says so.
            migrationBuilder.DropColumn(
                name: "CompletionPercent",
                schema: "game",
                table: "StructureInstances");

            migrationBuilder.RenameColumn(
                name: "BuildPoints",
                schema: "structure",
                table: "Structures",
                newName: "BuildPointsRequired");

            migrationBuilder.AddColumn<long>(
                name: "BuildPointsProgress",
                schema: "game",
                table: "StructureInstances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildPointsProgress",
                schema: "game",
                table: "StructureInstances");

            migrationBuilder.RenameColumn(
                name: "BuildPointsRequired",
                schema: "structure",
                table: "Structures",
                newName: "BuildPoints");

            migrationBuilder.AddColumn<double>(
                name: "CompletionPercent",
                schema: "game",
                table: "StructureInstances",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
