using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Renames the TroopInstance entity to UnitInstance, together with its table, its TroopName column and every index and
    /// constraint named after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-edited after scaffolding, deliberately.</b> EF could not infer a table rename (the CLR type name changed too), so it
    /// scaffolded <c>DropTable("TroopInstances")</c> followed by <c>CreateTable("UnitInstances")</c> — which would have destroyed
    /// every fielded unit in every game, and is what the "may result in the loss of data" warning was reporting. Both halves are
    /// replaced with <c>RenameTable</c> plus the constraint and index renames EF omits when it takes the drop-and-create path.
    /// </para>
    /// <para>
    /// The primary key and the four outgoing foreign keys are renamed through <c>sp_rename</c>, because MigrationBuilder exposes no
    /// rename for either. They are schema-scoped objects, so the old names still resolve after the table itself has been renamed.
    /// </para>
    /// </remarks>
    public partial class TM6_RenameTroopInstanceToUnitInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The inbound foreign keys carry the old table name, so they are dropped and re-added rather than renamed.
            migrationBuilder.DropForeignKey(
                name: "FK_TroopOrderParticipants_TroopInstances_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopOrders_TroopInstances_TroopInstanceId",
                schema: "troops",
                table: "TroopOrders");

            // --- The entity's own table, column, key, constraints and indexes -------------------------------------------
            migrationBuilder.RenameTable(
                name: "TroopInstances",
                schema: "units",
                newName: "UnitInstances",
                newSchema: "units");

            migrationBuilder.RenameColumn(
                name: "TroopName",
                schema: "units",
                table: "UnitInstances",
                newName: "Name");

            migrationBuilder.Sql("EXEC sp_rename N'units.PK_TroopInstances', N'PK_UnitInstances', N'OBJECT';");

            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopInstances_CampaignMap_CampaignMapTileId', N'FK_UnitInstances_CampaignMap_CampaignMapTileId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopInstances_GameIndex_GameId', N'FK_UnitInstances_GameIndex_GameId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopInstances_Player_PlayerId', N'FK_UnitInstances_Player_PlayerId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_TroopInstances_UnitTemplate_UnitTemplateId', N'FK_UnitInstances_UnitTemplate_UnitTemplateId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_CampaignMapTileId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_UnitInstances_CampaignMapTileId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_GameId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_UnitInstances_GameId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_PlayerId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_UnitInstances_PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_UnitTemplateId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_UnitInstances_UnitTemplateId");

            // --- The referencing columns and indexes on the order tables ------------------------------------------------
            migrationBuilder.RenameColumn(
                name: "TroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "UnitInstanceId");

            migrationBuilder.RenameColumn(
                name: "DefendTargetTroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "DefendTargetUnitInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_UnitInstanceId_OrderStatus");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_TroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_UnitInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_GameId_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_GameId_UnitInstanceId_OrderStatus");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_DefendTargetTroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_DefendTargetUnitInstanceId");

            migrationBuilder.RenameColumn(
                name: "TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "UnitInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "IX_TroopOrderParticipants_TroopOrderId_UnitInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrderParticipants_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "IX_TroopOrderParticipants_UnitInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopOrderParticipants_UnitInstances_UnitInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "UnitInstanceId",
                principalSchema: "units",
                principalTable: "UnitInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopOrders_UnitInstances_UnitInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "UnitInstanceId",
                principalSchema: "units",
                principalTable: "UnitInstances",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TroopOrderParticipants_UnitInstances_UnitInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopOrders_UnitInstances_UnitInstanceId",
                schema: "troops",
                table: "TroopOrders");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrderParticipants_UnitInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "IX_TroopOrderParticipants_TroopInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId_UnitInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "IX_TroopOrderParticipants_TroopOrderId_TroopInstanceId");

            migrationBuilder.RenameColumn(
                name: "UnitInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                newName: "TroopInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_DefendTargetUnitInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_DefendTargetTroopInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_GameId_UnitInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_GameId_TroopInstanceId_OrderStatus");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_UnitInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_TroopInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopOrders_UnitInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                newName: "IX_TroopOrders_TroopInstanceId_OrderStatus");

            migrationBuilder.RenameColumn(
                name: "DefendTargetUnitInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "DefendTargetTroopInstanceId");

            migrationBuilder.RenameColumn(
                name: "UnitInstanceId",
                schema: "troops",
                table: "TroopOrders",
                newName: "TroopInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitInstances_UnitTemplateId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_TroopInstances_UnitTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitInstances_PlayerId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_TroopInstances_PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitInstances_GameId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_TroopInstances_GameId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitInstances_CampaignMapTileId",
                schema: "units",
                table: "UnitInstances",
                newName: "IX_TroopInstances_CampaignMapTileId");

            migrationBuilder.Sql("EXEC sp_rename N'units.FK_UnitInstances_UnitTemplate_UnitTemplateId', N'FK_TroopInstances_UnitTemplate_UnitTemplateId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_UnitInstances_Player_PlayerId', N'FK_TroopInstances_Player_PlayerId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_UnitInstances_GameIndex_GameId', N'FK_TroopInstances_GameIndex_GameId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'units.FK_UnitInstances_CampaignMap_CampaignMapTileId', N'FK_TroopInstances_CampaignMap_CampaignMapTileId', N'OBJECT';");

            migrationBuilder.Sql("EXEC sp_rename N'units.PK_UnitInstances', N'PK_TroopInstances', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "units",
                table: "UnitInstances",
                newName: "TroopName");

            migrationBuilder.RenameTable(
                name: "UnitInstances",
                schema: "units",
                newName: "TroopInstances",
                newSchema: "units");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopOrderParticipants_TroopInstances_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopInstanceId",
                principalSchema: "units",
                principalTable: "TroopInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopOrders_TroopInstances_TroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TroopInstanceId",
                principalSchema: "units",
                principalTable: "TroopInstances",
                principalColumn: "Id");
        }
    }
}
