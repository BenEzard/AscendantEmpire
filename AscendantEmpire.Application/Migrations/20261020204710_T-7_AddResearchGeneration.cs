using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class T7_AddResearchGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastResearchGeneratedOnRound",
                schema: "technology",
                table: "PlayerResearchState",
                type: "int",
                nullable: true);

            // Added nullable, backfilled, then tightened, rather than added non-nullable with a default. A non-nullable add needs a default value for the
            // rows already there, and any default this column could take would be left behind on the table as a DEFAULT constraint the model does not
            // know about. Scaffolding it straight would also have landed every existing row on zero and then failed on the check constraint below.
            migrationBuilder.AddColumn<int>(
                name: "TechPointsPerPopulationSize",
                schema: "technology",
                table: "PlayerResearchState",
                type: "int",
                nullable: true);

            // The same opening figure GameSetupService writes for a new Game, so a Game created before this migration researches at the same rate as one
            // created after it.
            migrationBuilder.Sql("UPDATE [technology].[PlayerResearchState] SET [TechPointsPerPopulationSize] = 100;");

            migrationBuilder.AlterColumn<int>(
                name: "TechPointsPerPopulationSize",
                schema: "technology",
                table: "PlayerResearchState",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerResearchState_TechPointsPerPopulationSizePositive",
                schema: "technology",
                table: "PlayerResearchState",
                sql: "[TechPointsPerPopulationSize] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerResearchState_TechPointsPerPopulationSizePositive",
                schema: "technology",
                table: "PlayerResearchState");

            migrationBuilder.DropColumn(
                name: "LastResearchGeneratedOnRound",
                schema: "technology",
                table: "PlayerResearchState");

            migrationBuilder.DropColumn(
                name: "TechPointsPerPopulationSize",
                schema: "technology",
                table: "PlayerResearchState");
        }
    }
}
