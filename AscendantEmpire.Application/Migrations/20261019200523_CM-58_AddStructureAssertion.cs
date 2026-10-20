using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Adds <c>structure.Structures.Asserts</c>: what a completed Structure of each kind asserts over the tile it stands on.
    /// </summary>
    /// <remarks>
    /// <b>Hand-corrected after scaffolding, and re-scaffolding reintroduces the fault.</b> The scaffolder writes <c>defaultValue: 0</c> for a new non-nullable enum
    /// column, but 0 is not a legal <c>StructureAssertion</c> - the enum starts at 1 precisely so that nothing can assert "nothing" - so the backfill would put every
    /// existing catalogue row in violation and <c>CK_Structures_Asserts</c> would then refuse to be created. The default is 2, <c>StructureAssertion.Owned</c>, which is
    /// what every kind already behaved as, so this migration on its own changes nothing in play.
    /// </remarks>
    public partial class CM58_AddStructureAssertion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Asserts",
                schema: "structure",
                table: "Structures",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Structures_Asserts",
                schema: "structure",
                table: "Structures",
                sql: "[Asserts] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Structures_Asserts",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "Asserts",
                schema: "structure",
                table: "Structures");
        }
    }
}
