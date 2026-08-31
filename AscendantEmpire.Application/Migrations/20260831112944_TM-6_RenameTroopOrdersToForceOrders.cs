using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Renames the three TroopOrder tables to ForceOrder and moves them from the <c>troops</c> schema into <c>units</c>, together with
    /// every column, key, constraint and index named after the old spelling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-edited after scaffolding, for the same reason as the UnitInstance rename.</b> EF cannot infer a rename when the CLR type
    /// name changes, and cannot infer a schema move at all, so it scaffolded three <c>DropTable</c> calls followed by three
    /// <c>CreateTable</c> calls — which would have destroyed every standing order, movement step and participant row. That is what its
    /// "may result in the loss of data" warning was reporting. All six are replaced with <c>RenameTable</c>.
    /// </para>
    /// <para>
    /// Primary keys and foreign keys are renamed through <c>sp_rename</c>, because MigrationBuilder exposes no rename for either.
    /// Ordering matters: the tables move first, so the index renames can address them at their new schema and name.
    /// </para>
    /// <para>
    /// <c>ForceOrderId1</c> is not a typo. <c>ForceOrderParticipant</c> carries a shadow foreign key because its explicit
    /// <c>HasOne(...).WithMany()</c> configuration and <c>ForceOrder.Participants</c> are mapped as two separate relationships, so EF
    /// materialises a second, always-null column. That predates this work and is carried across unchanged rather than quietly fixed —
    /// removing it is a model change, not a rename.
    /// </para>
    /// </remarks>
    public partial class TM6_RenameTroopOrdersToForceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Tables: renamed and moved from the retired "troops" schema into "units" --------------------------------
            migrationBuilder.RenameTable(
                name: "TroopOrders",
                schema: "troops",
                newName: "ForceOrders",
                newSchema: "units");

            migrationBuilder.RenameTable(
                name: "TroopOrderMovementSteps",
                schema: "troops",
                newName: "ForceOrderMovementSteps",
                newSchema: "units");

            migrationBuilder.RenameTable(
                name: "TroopOrderParticipants",
                schema: "troops",
                newName: "ForceOrderParticipants",
                newSchema: "units");

            // --- Columns -------------------------------------------------------------------------------------------------
            migrationBuilder.RenameColumn(
                name: "TroopOrderId",
                schema: "units",
                table: "ForceOrderMovementSteps",
                newName: "ForceOrderId");

            migrationBuilder.RenameColumn(
                name: "TroopOrderId",
                schema: "units",
                table: "ForceOrderParticipants",
                newName: "ForceOrderId");

            migrationBuilder.RenameColumn(
                name: "TroopOrderId1",
                schema: "units",
                table: "ForceOrderParticipants",
                newName: "ForceOrderId1");

            // --- Primary keys --------------------------------------------------------------------------------------------
            migrationBuilder.Sql("EXEC sp_rename N'units.PK_TroopOrders', N'PK_ForceOrders', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.PK_TroopOrderMovementSteps', N'PK_ForceOrderMovementSteps', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.PK_TroopOrderParticipants', N'PK_ForceOrderParticipants', N'OBJECT';");

            // --- Foreign keys --------------------------------------------------------------------------------------------
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopOrders_UnitInstances_UnitInstanceId', N'FK_ForceOrders_UnitInstances_UnitInstanceId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopOrderMovementSteps_TroopOrders_TroopOrderId', N'FK_ForceOrderMovementSteps_ForceOrders_ForceOrderId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopOrderParticipants_TroopOrders_TroopOrderId', N'FK_ForceOrderParticipants_ForceOrders_ForceOrderId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopOrderParticipants_TroopOrders_TroopOrderId1', N'FK_ForceOrderParticipants_ForceOrders_ForceOrderId1', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopOrderParticipants_UnitInstances_UnitInstanceId', N'FK_ForceOrderParticipants_UnitInstances_UnitInstanceId', N'OBJECT';");

            // --- Indexes -------------------------------------------------------------------------------------------------
            RenameOrderIndexes(migrationBuilder, fromPrefix: "Troop", toPrefix: "Force");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameOrderIndexes(migrationBuilder, fromPrefix: "Force", toPrefix: "Troop");

            migrationBuilder.Sql("EXEC sp_rename N'units.FK_ForceOrderParticipants_UnitInstances_UnitInstanceId', N'FK_TroopOrderParticipants_UnitInstances_UnitInstanceId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_ForceOrderParticipants_ForceOrders_ForceOrderId1', N'FK_TroopOrderParticipants_TroopOrders_TroopOrderId1', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_ForceOrderParticipants_ForceOrders_ForceOrderId', N'FK_TroopOrderParticipants_TroopOrders_TroopOrderId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_ForceOrderMovementSteps_ForceOrders_ForceOrderId', N'FK_TroopOrderMovementSteps_TroopOrders_TroopOrderId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_ForceOrders_UnitInstances_UnitInstanceId', N'FK_TroopOrders_UnitInstances_UnitInstanceId', N'OBJECT';");

            migrationBuilder.Sql("EXEC sp_rename N'units.PK_ForceOrderParticipants', N'PK_TroopOrderParticipants', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.PK_ForceOrderMovementSteps', N'PK_TroopOrderMovementSteps', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.PK_ForceOrders', N'PK_TroopOrders', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants",
                newName: "TroopOrderId1");

            migrationBuilder.RenameColumn(
                name: "ForceOrderId",
                schema: "units",
                table: "ForceOrderParticipants",
                newName: "TroopOrderId");

            migrationBuilder.RenameColumn(
                name: "ForceOrderId",
                schema: "units",
                table: "ForceOrderMovementSteps",
                newName: "TroopOrderId");

            migrationBuilder.EnsureSchema(name: "troops");

            migrationBuilder.RenameTable(
                name: "ForceOrderParticipants",
                schema: "units",
                newName: "TroopOrderParticipants",
                newSchema: "troops");

            migrationBuilder.RenameTable(
                name: "ForceOrderMovementSteps",
                schema: "units",
                newName: "TroopOrderMovementSteps",
                newSchema: "troops");

            migrationBuilder.RenameTable(
                name: "ForceOrders",
                schema: "units",
                newName: "TroopOrders",
                newSchema: "troops");
        }

        /// <summary>
        /// Renames all twenty indexes across the three order tables in one direction.
        /// </summary>
        /// <remarks>
        /// Written as a loop rather than forty literal calls so the two directions cannot drift apart, and so a missed index is a
        /// missing list entry rather than a silently absent statement. It runs after the tables have moved, so every index is
        /// addressed at its new schema and table name.
        /// </remarks>
        private static void RenameOrderIndexes(MigrationBuilder migrationBuilder, string fromPrefix, string toPrefix)
        {
            (string Table, string[] Suffixes)[] indexesByTable =
            [
                ("OrderMovementSteps", ["CampaignMapTileId_IsCompleted", "{0}OrderId", "{0}OrderId_StepIndex"]),
                ("OrderParticipants", ["{0}OrderId", "{0}OrderId_UnitInstanceId", "{0}OrderId1", "UnitInstanceId"]),
                ("Orders",
                [
                    "DefendTargetStructureInstanceId",
                    "DefendTargetUnitInstanceId",
                    "GameId",
                    "GameId_OrderStatus",
                    "GameId_OrderType_OrderStatus",
                    "GameId_UnitInstanceId_OrderStatus",
                    "ResourceDefinitionId",
                    "StructureInstanceId",
                    "TargetCampaignMapTileId",
                    "TradeSourceStructureInstanceId",
                    "TradeTargetStructureInstanceId",
                    "UnitInstanceId",
                    "UnitInstanceId_OrderStatus",
                ]),
            ];

            foreach ((string table, string[] suffixes) in indexesByTable)
            {
                // Both directions address the table by its post-Up name, and that is not a slip. Up renames the tables before calling
                // this; Down calls this before renaming them back. Either way the table is "Force..." while the indexes are renamed.
                string currentTable = "Force" + table;

                foreach (string suffix in suffixes)
                {
                    string oldName = $"IX_{fromPrefix}{table}_{string.Format(suffix, fromPrefix)}";
                    string newName = $"IX_{toPrefix}{table}_{string.Format(suffix, toPrefix)}";

                    migrationBuilder.RenameIndex(
                        name: oldName,
                        schema: "units",
                        table: currentTable,
                        newName: newName);
                }
            }
        }
    }
}
