using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Splits the single movement column on <c>units.UnitInstances</c> into a campaign and a battle pool, and adds the Round stamp that makes
    /// start-of-Turn replenishment idempotent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generated SQL was checked by hand before this was committed. EF has no rename detection for properties in the general case — the TM-6 table
    /// rename had to be corrected after it scaffolded a destructive <c>DropTable</c>/<c>CreateTable</c> — so the risk here was a
    /// <c>DropColumn</c>/<c>AddColumn</c> pair that would have zeroed every fielded Unit's movement. It did not happen: the model differ produced a
    /// single <c>sp_rename</c>, which preserves the data. Verify that again before applying if this migration is ever regenerated.
    /// </para>
    /// <para>
    /// <c>MovementPointsRemaining</c> carried no default constraint of its own, so there is no stale auto-named <c>DF__</c> constraint left pointing at
    /// the old column name — the problem <c>TM-6_RenameStaleDefaultConstraints</c> exists to clean up.
    /// </para>
    /// <para>
    /// Both new columns default to 0. Existing rows are correct at that value: battle movement is granted only when a Unit is fielded, and a stamp of 0
    /// means "never replenished", so every pre-existing Force is granted its allowance the first time its owner's Turn opens.
    /// </para>
    /// </remarks>
    public partial class F14_RenameUnitMovementColumnsAndAddReplenishStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MovementPointsRemaining",
                schema: "units",
                table: "UnitInstances",
                newName: "CampaignMovementRemaining");

            migrationBuilder.AddColumn<long>(
                name: "BattleMovementRemaining",
                schema: "units",
                table: "UnitInstances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "CampaignMovementReplenishedOnRound",
                schema: "units",
                table: "UnitInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BattleMovementRemaining",
                schema: "units",
                table: "UnitInstances");

            migrationBuilder.DropColumn(
                name: "CampaignMovementReplenishedOnRound",
                schema: "units",
                table: "UnitInstances");

            migrationBuilder.RenameColumn(
                name: "CampaignMovementRemaining",
                schema: "units",
                table: "UnitInstances",
                newName: "MovementPointsRemaining");
        }
    }
}
