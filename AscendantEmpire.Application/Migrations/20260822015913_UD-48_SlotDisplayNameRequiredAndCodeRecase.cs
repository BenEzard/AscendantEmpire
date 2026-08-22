using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD48_SlotDisplayNameRequiredAndCodeRecase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A slot's Code is now slugified from its DisplayName by the Entity Setup Tool, so the name has to
            // be present. Name the unnamed ones before the column is tightened — "Slot 1", "Slot 2", … per
            // body, matching what a newly added slot is called in the tool.
            //
            // Two slots on one body can end up with the same name here (if a body already had a slot literally
            // called "Slot 1"). That is deliberately left to surface in the tool rather than guessed at: names
            // are not constrained by the database, and Codes are recased below rather than re-derived, so no
            // unique index is at risk.
            migrationBuilder.Sql("""
                WITH NumberedSlots AS
                (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY UnitComponentBodyId ORDER BY Id) AS Ordinal
                    FROM unitdesigns.UnitComponentSlot
                )
                UPDATE slot
                SET slot.DisplayName = CONCAT('Slot ', numbered.Ordinal)
                FROM unitdesigns.UnitComponentSlot AS slot
                INNER JOIN NumberedSlots AS numbered ON numbered.Id = slot.Id
                WHERE slot.DisplayName IS NULL OR LTRIM(RTRIM(slot.DisplayName)) = '';
                """);

            // No defaultValue: the backfill above is the only thing that should be filling names. If a NULL
            // somehow survives it, failing here is better than silently writing an empty identity source.
            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // Bring existing codes onto the shared convention (lowercase, hyphen-separated) that component and
            // tag codes already use, so the tool's derivation matches what is already stored.
            //
            // Safe as a 1:1 mapping: the old editor only ever admitted [A-Z0-9_], and '_' maps to '-', so no
            // two existing codes on a body can collapse into one and trip UX_UnitComponentSlot_Body_Code.
            // Nothing references these values — UnitTemplateComponent pins slots by Id, never by code — so no
            // saved template is disturbed.
            migrationBuilder.Sql("""
                UPDATE unitdesigns.UnitComponentSlot
                SET Code = LOWER(REPLACE(Code, '_', '-'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put the codes back on the old uppercase convention. The generated display names cannot be
            // reverted — which slots had none is not recorded — so they stay; the column simply goes back to
            // allowing NULL.
            migrationBuilder.Sql("""
                UPDATE unitdesigns.UnitComponentSlot
                SET Code = UPPER(REPLACE(Code, '-', '_'));
                """);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
