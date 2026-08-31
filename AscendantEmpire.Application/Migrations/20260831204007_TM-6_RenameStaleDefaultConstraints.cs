using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Renames the two default constraints left carrying the retired <c>TroopOrder</c> spelling after the tables beneath them were renamed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HasDefaultValue</c> on <c>ForceOrderMovementSteps.IsCompleted</c> and <c>ForceOrders.WorkCompleted</c> creates the constraint without
    /// naming it, so SQL Server generated <c>DF__TroopOrde__IsCom__32AB8735</c> and <c>DF__TroopOrde__WorkC__2EDAF651</c> from the table names of
    /// the day. <c>sp_rename</c> on a table does not cascade into them, so the earlier rename left them behind. Found by reading
    /// <c>sys.default_constraints</c> after the migration was applied, not by the scaffolder.
    /// </para>
    /// <para>
    /// <b>Cosmetic only.</b> EF never addresses a default constraint by name — its own <c>AlterColumn</c> output looks the name up from
    /// <c>sys.default_constraints</c> at run time — so nothing was broken. What was wrong is that the database still said "Troop" in two places
    /// after a rename whose whole point was that it should not.
    /// </para>
    /// <para>
    /// The old names are looked up rather than hard-coded, because the trailing hash is generated per database: the literal names above are this
    /// developer's, and another instance will have different ones for the same two constraints.
    /// </para>
    /// </remarks>
    public partial class TM6_RenameStaleDefaultConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RenameDefaultConstraint(migrationBuilder, "ForceOrderMovementSteps", "IsCompleted");
            RenameDefaultConstraint(migrationBuilder, "ForceOrders", "WorkCompleted");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deliberately empty. The names this migration replaces were server-generated with a per-database hash suffix and cannot be
        /// reconstructed, so there is nothing to restore. Re-running <see cref="Up"/> after a rollback is safe regardless, because it renames
        /// whatever constraint it finds on the column rather than one it expects by name.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }

        /// <summary>
        /// Gives the default constraint on one column a deterministic <c>DF_{table}_{column}</c> name, whatever it is called now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A no-op when the constraint is already correctly named or does not exist, so the migration is safe to apply to a database built from
        /// this migration set and to one that predates it.
        /// </para>
        /// <para>
        /// The variables are suffixed per constraint rather than sharing one name, because every call's SQL lands in the <b>same batch</b> and
        /// T-SQL scopes <c>DECLARE</c> to the batch, not to the surrounding <c>BEGIN</c>/<c>END</c>. Two calls sharing <c>@currentName</c> fail
        /// the whole migration with "the variable name has already been declared".
        /// </para>
        /// </remarks>
        private static void RenameDefaultConstraint(MigrationBuilder migrationBuilder, string table, string column)
        {
            string targetName = $"DF_{table}_{column}";
            string suffix = $"{table}_{column}";

            migrationBuilder.Sql($"""
                DECLARE @current_{suffix} sysname;

                SELECT @current_{suffix} = dc.name
                FROM sys.default_constraints dc
                JOIN sys.tables t ON t.object_id = dc.parent_object_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                WHERE s.name = N'units' AND t.name = N'{table}' AND c.name = N'{column}';

                IF @current_{suffix} IS NOT NULL AND @current_{suffix} <> N'{targetName}'
                BEGIN
                    DECLARE @qualified_{suffix} nvarchar(400) = N'units.' + QUOTENAME(@current_{suffix});
                    EXEC sp_rename @qualified_{suffix}, N'{targetName}', N'OBJECT';
                END
                """);
        }
    }
}
