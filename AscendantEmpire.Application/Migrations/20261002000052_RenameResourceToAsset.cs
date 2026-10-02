using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Renames the economy catalogue from Resource to Asset, and moves it from the <c>resources</c> schema to <c>assets</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written. The scaffolded version dropped and recreated all six catalogue tables, because EF models a schema move as
    /// a drop plus a create and cannot see that the tables either side are the same. That would have taken every catalogue row
    /// with it, along with the Player unlock rows and every MapResource yield, layer and scenario amount that names one. This
    /// version only renames: no table is created or dropped, and no row is touched.
    /// </para>
    /// <para>
    /// Constraints are renamed in place with <c>sp_rename</c>, which <c>MigrationBuilder</c> has no operation for — it can only
    /// drop and add a key or a foreign key, and dropping a primary key the rest of the schema points at is not possible while
    /// those references stand. The three check constraints whose own SQL text names a renamed column are the exception: their
    /// definition changes, so they are dropped and re-added.
    /// </para>
    /// <para>
    /// The emptied <c>resources</c> schema is deliberately left in place rather than dropped, so this migration cannot fail on
    /// a database that happens to hold anything else under it.
    /// </para>
    /// </remarks>
    public partial class RenameResourceToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "assets");

            // --- Check constraints naming a column that is about to be renamed ------------------------------------------
            // Dropped first, while the tables still carry their old names, and re-added at the end against the new ones.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Resource_GroupOrCategoryXor",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MapResourceInstanceLayer_Quantity",
                schema: "game",
                table: "MapResourceInstanceLayer");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayerMapResourceSwitch_KindTarget",
                schema: "game",
                table: "PlayerMapResourceSwitch");

            // --- The six catalogue tables: moved to the assets schema and renamed ---------------------------------------
            migrationBuilder.RenameTable(name: "ResourceMetaCategory", schema: "resources", newName: "AssetMetaCategory", newSchema: "assets");
            migrationBuilder.RenameTable(name: "ResourceCategory", schema: "resources", newName: "AssetCategory", newSchema: "assets");
            migrationBuilder.RenameTable(name: "ResourceGroup", schema: "resources", newName: "AssetGroup", newSchema: "assets");
            migrationBuilder.RenameTable(name: "Resource", schema: "resources", newName: "Asset", newSchema: "assets");
            migrationBuilder.RenameTable(name: "PlayerUnlockedResource", schema: "resources", newName: "PlayerUnlockedAsset", newSchema: "assets");
            migrationBuilder.RenameTable(name: "PlayerUnlockedResourceMetaCategory", schema: "resources", newName: "PlayerUnlockedAssetMetaCategory", newSchema: "assets");

            // --- Columns inside the catalogue ---------------------------------------------------------------------------
            migrationBuilder.RenameColumn(name: "ResourceMetaCategoryId", schema: "assets", table: "AssetCategory", newName: "AssetMetaCategoryId");
            migrationBuilder.RenameColumn(name: "ResourceCategoryId", schema: "assets", table: "AssetGroup", newName: "AssetCategoryId");
            migrationBuilder.RenameColumn(name: "ResourceMetaCategoryId", schema: "assets", table: "Asset", newName: "AssetMetaCategoryId");
            migrationBuilder.RenameColumn(name: "ResourceGroupId", schema: "assets", table: "Asset", newName: "AssetGroupId");
            migrationBuilder.RenameColumn(name: "ResourceCategoryId", schema: "assets", table: "Asset", newName: "AssetCategoryId");
            migrationBuilder.RenameColumn(name: "IsGradedResource", schema: "assets", table: "Asset", newName: "IsGradedAsset");
            migrationBuilder.RenameColumn(name: "ResourceGrade", schema: "assets", table: "Asset", newName: "AssetGrade");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "assets", table: "PlayerUnlockedAsset", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceMetaCategoryId", schema: "assets", table: "PlayerUnlockedAssetMetaCategory", newName: "AssetMetaCategoryId");

            // --- The economy foreign key on each MapResource table ------------------------------------------------------
            // The tables themselves are map-side and keep their names; only the column changes.
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "map", table: "MapResourceTypeYield", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "map", table: "MapResourceTypeLayer", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "map", table: "MapResourceDistributionScenarioAmount", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "game", table: "MapResourceInstanceAmount", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "game", table: "MapResourceInstanceLayer", newName: "AssetId");
            migrationBuilder.RenameColumn(name: "ResourceId", schema: "game", table: "PlayerMapResourceSwitch", newName: "AssetId");

            // --- Indexes inside the catalogue ---------------------------------------------------------------------------
            migrationBuilder.RenameIndex(name: "IX_ResourceMetaCategory_Code", schema: "assets", table: "AssetMetaCategory", newName: "IX_AssetMetaCategory_Code");
            migrationBuilder.RenameIndex(name: "IX_ResourceMetaCategory_DisplayName", schema: "assets", table: "AssetMetaCategory", newName: "IX_AssetMetaCategory_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_ResourceCategory_Code", schema: "assets", table: "AssetCategory", newName: "IX_AssetCategory_Code");
            migrationBuilder.RenameIndex(name: "IX_ResourceCategory_ResourceMetaCategoryId_DisplayName", schema: "assets", table: "AssetCategory", newName: "IX_AssetCategory_AssetMetaCategoryId_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_ResourceGroup_Code", schema: "assets", table: "AssetGroup", newName: "IX_AssetGroup_Code");
            migrationBuilder.RenameIndex(name: "IX_ResourceGroup_ResourceCategoryId_DisplayName", schema: "assets", table: "AssetGroup", newName: "IX_AssetGroup_AssetCategoryId_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_ResourceGroup_ResourceCategoryId_SortOrder", schema: "assets", table: "AssetGroup", newName: "IX_AssetGroup_AssetCategoryId_SortOrder");
            migrationBuilder.RenameIndex(name: "IX_Resource_Code", schema: "assets", table: "Asset", newName: "IX_Asset_Code");
            migrationBuilder.RenameIndex(name: "IX_Resource_DisplayName", schema: "assets", table: "Asset", newName: "IX_Asset_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_Resource_ResourceCategoryId", schema: "assets", table: "Asset", newName: "IX_Asset_AssetCategoryId");
            migrationBuilder.RenameIndex(name: "IX_Resource_ResourceGroupId", schema: "assets", table: "Asset", newName: "IX_Asset_AssetGroupId");
            migrationBuilder.RenameIndex(name: "IX_Resource_ResourceMetaCategoryId", schema: "assets", table: "Asset", newName: "IX_Asset_AssetMetaCategoryId");
            migrationBuilder.RenameIndex(name: "IX_PlayerUnlockedResource_Resource", schema: "assets", table: "PlayerUnlockedAsset", newName: "IX_PlayerUnlockedAsset_Asset");
            migrationBuilder.RenameIndex(name: "IX_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory", schema: "assets", table: "PlayerUnlockedAssetMetaCategory", newName: "IX_PlayerUnlockedAssetMetaCategory_AssetMetaCategory");

            // --- Indexes over the economy foreign key on the MapResource tables -----------------------------------------
            migrationBuilder.RenameIndex(name: "IX_MapResourceTypeYield_ResourceId", schema: "map", table: "MapResourceTypeYield", newName: "IX_MapResourceTypeYield_AssetId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceTypeLayer_ResourceId", schema: "map", table: "MapResourceTypeLayer", newName: "IX_MapResourceTypeLayer_AssetId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceDistributionScenarioAmount_ResourceId", schema: "map", table: "MapResourceDistributionScenarioAmount", newName: "IX_MapResourceDistributionScenarioAmount_AssetId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceInstanceAmount_ResourceId", schema: "game", table: "MapResourceInstanceAmount", newName: "IX_MapResourceInstanceAmount_AssetId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceInstanceLayer_ResourceId", schema: "game", table: "MapResourceInstanceLayer", newName: "IX_MapResourceInstanceLayer_AssetId");
            migrationBuilder.RenameIndex(name: "IX_PlayerMapResourceSwitch_ResourceId", schema: "game", table: "PlayerMapResourceSwitch", newName: "IX_PlayerMapResourceSwitch_AssetId");
            migrationBuilder.RenameIndex(
                name: "UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Resource_Layer",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                newName: "UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Asset_Layer");

            // --- Primary keys -------------------------------------------------------------------------------------------
            RenameConstraint(migrationBuilder, "assets", "PK_ResourceMetaCategory", "PK_AssetMetaCategory");
            RenameConstraint(migrationBuilder, "assets", "PK_ResourceCategory", "PK_AssetCategory");
            RenameConstraint(migrationBuilder, "assets", "PK_ResourceGroup", "PK_AssetGroup");
            RenameConstraint(migrationBuilder, "assets", "PK_Resource", "PK_Asset");
            RenameConstraint(migrationBuilder, "assets", "PK_PlayerUnlockedResource", "PK_PlayerUnlockedAsset");
            RenameConstraint(migrationBuilder, "assets", "PK_PlayerUnlockedResourceMetaCategory", "PK_PlayerUnlockedAssetMetaCategory");

            // --- Foreign keys inside the catalogue ----------------------------------------------------------------------
            RenameConstraint(migrationBuilder, "assets", "FK_ResourceCategory_ResourceMetaCategory_ResourceMetaCategoryId", "FK_AssetCategory_AssetMetaCategory_AssetMetaCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_ResourceGroup_ResourceCategory_ResourceCategoryId", "FK_AssetGroup_AssetCategory_AssetCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_Resource_ResourceCategory_ResourceCategoryId", "FK_Asset_AssetCategory_AssetCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_Resource_ResourceGroup_ResourceGroupId", "FK_Asset_AssetGroup_AssetGroupId");
            RenameConstraint(migrationBuilder, "assets", "FK_Resource_ResourceMetaCategory_ResourceMetaCategoryId", "FK_Asset_AssetMetaCategory_AssetMetaCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedResource_Player_PlayerId", "FK_PlayerUnlockedAsset_Player_PlayerId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedResource_Resource_ResourceId", "FK_PlayerUnlockedAsset_Asset_AssetId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedResourceMetaCategory_Player_PlayerId", "FK_PlayerUnlockedAssetMetaCategory_Player_PlayerId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory_ResourceMetaCategoryId", "FK_PlayerUnlockedAssetMetaCategory_AssetMetaCategory_AssetMetaCategoryId");

            // --- Foreign keys from the MapResource tables into the catalogue --------------------------------------------
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceTypeYield_Resource_ResourceId", "FK_MapResourceTypeYield_Asset_AssetId");
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceTypeLayer_Resource_ResourceId", "FK_MapResourceTypeLayer_Asset_AssetId");
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceDistributionScenarioAmount_Resource_ResourceId", "FK_MapResourceDistributionScenarioAmount_Asset_AssetId");
            RenameConstraint(migrationBuilder, "game", "FK_MapResourceInstanceAmount_Resource_ResourceId", "FK_MapResourceInstanceAmount_Asset_AssetId");
            RenameConstraint(migrationBuilder, "game", "FK_MapResourceInstanceLayer_Resource_ResourceId", "FK_MapResourceInstanceLayer_Asset_AssetId");
            RenameConstraint(migrationBuilder, "game", "FK_PlayerMapResourceSwitch_Resource_ResourceId", "FK_PlayerMapResourceSwitch_Asset_AssetId");

            // --- The three check constraints, re-added over the renamed columns -----------------------------------------
            migrationBuilder.AddCheckConstraint(
                name: "CK_Asset_GroupOrCategoryXor",
                schema: "assets",
                table: "Asset",
                sql: "([AssetGroupId] IS NOT NULL AND [AssetCategoryId] IS NULL) OR ([AssetGroupId] IS NULL AND [AssetCategoryId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MapResourceInstanceLayer_Quantity",
                schema: "game",
                table: "MapResourceInstanceLayer",
                sql: "[Quantity] >= 0 AND ([AssetId] IS NOT NULL OR [Quantity] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerMapResourceSwitch_KindTarget",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                sql: "([Kind] = 1 AND [AssetId] IS NULL AND [LayerNumber] IS NULL) OR ([Kind] = 2 AND [AssetId] IS NOT NULL AND [LayerNumber] IS NULL) OR ([Kind] = 3 AND [AssetId] IS NULL AND [LayerNumber] >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "resources");

            migrationBuilder.DropCheckConstraint(name: "CK_Asset_GroupOrCategoryXor", schema: "assets", table: "Asset");
            migrationBuilder.DropCheckConstraint(name: "CK_MapResourceInstanceLayer_Quantity", schema: "game", table: "MapResourceInstanceLayer");
            migrationBuilder.DropCheckConstraint(name: "CK_PlayerMapResourceSwitch_KindTarget", schema: "game", table: "PlayerMapResourceSwitch");

            RenameConstraint(migrationBuilder, "game", "FK_PlayerMapResourceSwitch_Asset_AssetId", "FK_PlayerMapResourceSwitch_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "game", "FK_MapResourceInstanceLayer_Asset_AssetId", "FK_MapResourceInstanceLayer_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "game", "FK_MapResourceInstanceAmount_Asset_AssetId", "FK_MapResourceInstanceAmount_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceDistributionScenarioAmount_Asset_AssetId", "FK_MapResourceDistributionScenarioAmount_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceTypeLayer_Asset_AssetId", "FK_MapResourceTypeLayer_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "map", "FK_MapResourceTypeYield_Asset_AssetId", "FK_MapResourceTypeYield_Resource_ResourceId");

            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedAssetMetaCategory_AssetMetaCategory_AssetMetaCategoryId", "FK_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory_ResourceMetaCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedAssetMetaCategory_Player_PlayerId", "FK_PlayerUnlockedResourceMetaCategory_Player_PlayerId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedAsset_Asset_AssetId", "FK_PlayerUnlockedResource_Resource_ResourceId");
            RenameConstraint(migrationBuilder, "assets", "FK_PlayerUnlockedAsset_Player_PlayerId", "FK_PlayerUnlockedResource_Player_PlayerId");
            RenameConstraint(migrationBuilder, "assets", "FK_Asset_AssetMetaCategory_AssetMetaCategoryId", "FK_Resource_ResourceMetaCategory_ResourceMetaCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_Asset_AssetGroup_AssetGroupId", "FK_Resource_ResourceGroup_ResourceGroupId");
            RenameConstraint(migrationBuilder, "assets", "FK_Asset_AssetCategory_AssetCategoryId", "FK_Resource_ResourceCategory_ResourceCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_AssetGroup_AssetCategory_AssetCategoryId", "FK_ResourceGroup_ResourceCategory_ResourceCategoryId");
            RenameConstraint(migrationBuilder, "assets", "FK_AssetCategory_AssetMetaCategory_AssetMetaCategoryId", "FK_ResourceCategory_ResourceMetaCategory_ResourceMetaCategoryId");

            RenameConstraint(migrationBuilder, "assets", "PK_PlayerUnlockedAssetMetaCategory", "PK_PlayerUnlockedResourceMetaCategory");
            RenameConstraint(migrationBuilder, "assets", "PK_PlayerUnlockedAsset", "PK_PlayerUnlockedResource");
            RenameConstraint(migrationBuilder, "assets", "PK_Asset", "PK_Resource");
            RenameConstraint(migrationBuilder, "assets", "PK_AssetGroup", "PK_ResourceGroup");
            RenameConstraint(migrationBuilder, "assets", "PK_AssetCategory", "PK_ResourceCategory");
            RenameConstraint(migrationBuilder, "assets", "PK_AssetMetaCategory", "PK_ResourceMetaCategory");

            migrationBuilder.RenameIndex(
                name: "UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Asset_Layer",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                newName: "UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Resource_Layer");
            migrationBuilder.RenameIndex(name: "IX_PlayerMapResourceSwitch_AssetId", schema: "game", table: "PlayerMapResourceSwitch", newName: "IX_PlayerMapResourceSwitch_ResourceId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceInstanceLayer_AssetId", schema: "game", table: "MapResourceInstanceLayer", newName: "IX_MapResourceInstanceLayer_ResourceId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceInstanceAmount_AssetId", schema: "game", table: "MapResourceInstanceAmount", newName: "IX_MapResourceInstanceAmount_ResourceId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceDistributionScenarioAmount_AssetId", schema: "map", table: "MapResourceDistributionScenarioAmount", newName: "IX_MapResourceDistributionScenarioAmount_ResourceId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceTypeLayer_AssetId", schema: "map", table: "MapResourceTypeLayer", newName: "IX_MapResourceTypeLayer_ResourceId");
            migrationBuilder.RenameIndex(name: "IX_MapResourceTypeYield_AssetId", schema: "map", table: "MapResourceTypeYield", newName: "IX_MapResourceTypeYield_ResourceId");

            migrationBuilder.RenameIndex(name: "IX_PlayerUnlockedAssetMetaCategory_AssetMetaCategory", schema: "assets", table: "PlayerUnlockedAssetMetaCategory", newName: "IX_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory");
            migrationBuilder.RenameIndex(name: "IX_PlayerUnlockedAsset_Asset", schema: "assets", table: "PlayerUnlockedAsset", newName: "IX_PlayerUnlockedResource_Resource");
            migrationBuilder.RenameIndex(name: "IX_Asset_AssetMetaCategoryId", schema: "assets", table: "Asset", newName: "IX_Resource_ResourceMetaCategoryId");
            migrationBuilder.RenameIndex(name: "IX_Asset_AssetGroupId", schema: "assets", table: "Asset", newName: "IX_Resource_ResourceGroupId");
            migrationBuilder.RenameIndex(name: "IX_Asset_AssetCategoryId", schema: "assets", table: "Asset", newName: "IX_Resource_ResourceCategoryId");
            migrationBuilder.RenameIndex(name: "IX_Asset_DisplayName", schema: "assets", table: "Asset", newName: "IX_Resource_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_Asset_Code", schema: "assets", table: "Asset", newName: "IX_Resource_Code");
            migrationBuilder.RenameIndex(name: "IX_AssetGroup_AssetCategoryId_SortOrder", schema: "assets", table: "AssetGroup", newName: "IX_ResourceGroup_ResourceCategoryId_SortOrder");
            migrationBuilder.RenameIndex(name: "IX_AssetGroup_AssetCategoryId_DisplayName", schema: "assets", table: "AssetGroup", newName: "IX_ResourceGroup_ResourceCategoryId_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_AssetGroup_Code", schema: "assets", table: "AssetGroup", newName: "IX_ResourceGroup_Code");
            migrationBuilder.RenameIndex(name: "IX_AssetCategory_AssetMetaCategoryId_DisplayName", schema: "assets", table: "AssetCategory", newName: "IX_ResourceCategory_ResourceMetaCategoryId_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_AssetCategory_Code", schema: "assets", table: "AssetCategory", newName: "IX_ResourceCategory_Code");
            migrationBuilder.RenameIndex(name: "IX_AssetMetaCategory_DisplayName", schema: "assets", table: "AssetMetaCategory", newName: "IX_ResourceMetaCategory_DisplayName");
            migrationBuilder.RenameIndex(name: "IX_AssetMetaCategory_Code", schema: "assets", table: "AssetMetaCategory", newName: "IX_ResourceMetaCategory_Code");

            migrationBuilder.RenameColumn(name: "AssetId", schema: "game", table: "PlayerMapResourceSwitch", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "game", table: "MapResourceInstanceLayer", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "game", table: "MapResourceInstanceAmount", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "map", table: "MapResourceDistributionScenarioAmount", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "map", table: "MapResourceTypeLayer", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "map", table: "MapResourceTypeYield", newName: "ResourceId");

            migrationBuilder.RenameColumn(name: "AssetMetaCategoryId", schema: "assets", table: "PlayerUnlockedAssetMetaCategory", newName: "ResourceMetaCategoryId");
            migrationBuilder.RenameColumn(name: "AssetId", schema: "assets", table: "PlayerUnlockedAsset", newName: "ResourceId");
            migrationBuilder.RenameColumn(name: "AssetGrade", schema: "assets", table: "Asset", newName: "ResourceGrade");
            migrationBuilder.RenameColumn(name: "IsGradedAsset", schema: "assets", table: "Asset", newName: "IsGradedResource");
            migrationBuilder.RenameColumn(name: "AssetCategoryId", schema: "assets", table: "Asset", newName: "ResourceCategoryId");
            migrationBuilder.RenameColumn(name: "AssetGroupId", schema: "assets", table: "Asset", newName: "ResourceGroupId");
            migrationBuilder.RenameColumn(name: "AssetMetaCategoryId", schema: "assets", table: "Asset", newName: "ResourceMetaCategoryId");
            migrationBuilder.RenameColumn(name: "AssetCategoryId", schema: "assets", table: "AssetGroup", newName: "ResourceCategoryId");
            migrationBuilder.RenameColumn(name: "AssetMetaCategoryId", schema: "assets", table: "AssetCategory", newName: "ResourceMetaCategoryId");

            migrationBuilder.RenameTable(name: "PlayerUnlockedAssetMetaCategory", schema: "assets", newName: "PlayerUnlockedResourceMetaCategory", newSchema: "resources");
            migrationBuilder.RenameTable(name: "PlayerUnlockedAsset", schema: "assets", newName: "PlayerUnlockedResource", newSchema: "resources");
            migrationBuilder.RenameTable(name: "Asset", schema: "assets", newName: "Resource", newSchema: "resources");
            migrationBuilder.RenameTable(name: "AssetGroup", schema: "assets", newName: "ResourceGroup", newSchema: "resources");
            migrationBuilder.RenameTable(name: "AssetCategory", schema: "assets", newName: "ResourceCategory", newSchema: "resources");
            migrationBuilder.RenameTable(name: "AssetMetaCategory", schema: "assets", newName: "ResourceMetaCategory", newSchema: "resources");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Resource_GroupOrCategoryXor",
                schema: "resources",
                table: "Resource",
                sql: "([ResourceGroupId] IS NOT NULL AND [ResourceCategoryId] IS NULL) OR ([ResourceGroupId] IS NULL AND [ResourceCategoryId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MapResourceInstanceLayer_Quantity",
                schema: "game",
                table: "MapResourceInstanceLayer",
                sql: "[Quantity] >= 0 AND ([ResourceId] IS NOT NULL OR [Quantity] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayerMapResourceSwitch_KindTarget",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                sql: "([Kind] = 1 AND [ResourceId] IS NULL AND [LayerNumber] IS NULL) OR ([Kind] = 2 AND [ResourceId] IS NOT NULL AND [LayerNumber] IS NULL) OR ([Kind] = 3 AND [ResourceId] IS NULL AND [LayerNumber] >= 1)");
        }

        /// <summary>
        /// Renames a constraint in place, leaving the data and every reference to it alone.
        /// </summary>
        /// <remarks>
        /// <c>MigrationBuilder</c> has no rename for a key, foreign key or check constraint — only drop and add — and a primary
        /// key cannot be dropped while other tables' foreign keys point at it. <c>sp_rename</c> with <c>OBJECT</c> renames the
        /// constraint without touching what depends on it.
        /// </remarks>
        private static void RenameConstraint(MigrationBuilder migrationBuilder, string schema, string oldName, string newName)
        {
            migrationBuilder.Sql($"EXEC sp_rename N'[{schema}].[{oldName}]', N'{newName}', N'OBJECT';");
        }
    }
}
