using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class PopulateNumbersTable : Migration
    {
        // Data migration: seed util.Numbers with the inclusive range 0..1,000,000 (1,000,001 rows).
        // util.Numbers is a "tally"/"numbers" helper table whose Id is the value itself (no identity).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Build the values set-based via cascading cross joins rather than a loop or a
            // recursive CTE: this generates all 1,000,001 rows in a single pass and avoids the
            // default 100-level recursion limit. Each level squares the previous row count.
            //   E1 = 10 rows, E2 = 100, E4 = 10,000, E8 = 100,000,000 (more than enough).
            // ROW_NUMBER() - 1 yields 0..N; TOP (1000001) caps it at the value 1,000,000.
            // The NOT EXISTS guard keeps the insert safe to re-run if the table is partially seeded.
            migrationBuilder.Sql(
                """
                WITH
                    E1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) AS t(n)),
                    E2(n) AS (SELECT 1 FROM E1 a CROSS JOIN E1 b),
                    E4(n) AS (SELECT 1 FROM E2 a CROSS JOIN E2 b),
                    E8(n) AS (SELECT 1 FROM E4 a CROSS JOIN E4 b),
                    Tally(Id) AS
                    (
                        SELECT TOP (1000001) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1
                        FROM E8
                    )
                INSERT INTO util.Numbers (Id)
                SELECT t.Id
                FROM Tally t
                WHERE NOT EXISTS (SELECT 1 FROM util.Numbers n WHERE n.Id = t.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove exactly the range this migration seeded.
            migrationBuilder.Sql("DELETE FROM util.Numbers WHERE Id BETWEEN 0 AND 1000000;");
        }
    }
}
