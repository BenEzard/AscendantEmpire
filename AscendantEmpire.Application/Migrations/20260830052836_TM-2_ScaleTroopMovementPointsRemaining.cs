using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class TM2_ScaleTroopMovementPointsRemaining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Moves remaining movement points onto the raw fixed-point Scale 1,000 convention every other movement, power and hitpoint value
            // already uses, so a troop's remaining MP can be compared directly against its compiled template's CampaignMovementPoints allowance.
            //
            // Note this ALTER *truncates* rather than rescales — SQL Server converts decimal to bigint by dropping the fraction, so a stored 1.500
            // would arrive as 1, not 1500. That is safe here only because nothing has ever written this column: it is not set on spawn and is not
            // replenished at turn rollover, so every existing row holds 0.000 and there is no value to preserve. Should that change before this
            // migration is applied anywhere, the conversion must become a multiply-by-1000 instead.
            migrationBuilder.AlterColumn<long>(
                name: "MovementPointsRemaining",
                schema: "units",
                table: "TroopInstances",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "MovementPointsRemaining",
                schema: "units",
                table: "TroopInstances",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
