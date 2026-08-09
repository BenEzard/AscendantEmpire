using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD31_AddUnitComponentSlotCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Every existing row was added with the same empty default, which the unique index below would
            // reject for any body exposing more than one slot. Backfill a distinct code per slot first.
            //
            // Numbering follows ascending Id, which is the order the old positional SlotComponentCodes list
            // used — so SLOT_1 is the slot that was previously index 0, and existing templates keep pointing
            // at the slot they always did. These are placeholders: rename them to something meaningful
            // (MAIN_HAND, ARMOUR_1, ...) from the Entity Setup Tool, which sends the slot id alongside the
            // code so a rename updates the row in place rather than replacing it.
            migrationBuilder.Sql("""
                WITH [NumberedSlots] AS
                (
                    SELECT [Code],
                           ROW_NUMBER() OVER (PARTITION BY [UnitComponentBodyId] ORDER BY [Id]) AS [SlotNumber]
                    FROM [unitdesigns].[UnitComponentSlot]
                )
                UPDATE [NumberedSlots]
                SET [Code] = 'SLOT_' + CAST([SlotNumber] AS nvarchar(10));
                """);

            migrationBuilder.CreateIndex(
                name: "UX_UnitComponentSlot_Body_Code",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                columns: new[] { "UnitComponentBodyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UnitComponentSlot_Body_Code",
                schema: "unitdesigns",
                table: "UnitComponentSlot");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "unitdesigns",
                table: "UnitComponentSlot");
        }
    }
}
