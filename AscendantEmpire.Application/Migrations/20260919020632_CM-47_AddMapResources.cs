using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM47_AddMapResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapResourceCategory",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapResourceType",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MapResourceCategoryId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRenewable = table.Column<bool>(type: "bit", nullable: false),
                    IsStratified = table.Column<bool>(type: "bit", nullable: false),
                    IsDistribution = table.Column<bool>(type: "bit", nullable: false),
                    ExtractionMethod = table.Column<int>(type: "int", nullable: false),
                    CaptureLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RequiresResearchToExtract = table.Column<bool>(type: "bit", nullable: false),
                    IsDiscoverableByDefault = table.Column<bool>(type: "bit", nullable: false),
                    DiscoveryLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EstimationFuzzyFactorMin = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    EstimationFuzzyFactorMax = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SurvivalRounds = table.Column<int>(type: "int", nullable: true),
                    ReseedLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceType", x => x.Id);
                    table.CheckConstraint("CK_MapResourceType_DistributionIsRenewable", "[IsDistribution] = 0 OR [IsRenewable] = 1");
                    table.CheckConstraint("CK_MapResourceType_RenewableOrStratified", "[IsRenewable] = 0 OR [IsStratified] = 0");
                    table.ForeignKey(
                        name: "FK_MapResourceType_MapResourceCategory_MapResourceCategoryId",
                        column: x => x.MapResourceCategoryId,
                        principalSchema: "map",
                        principalTable: "MapResourceCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MapResourceDistributionScenario",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    ScenarioNumber = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceDistributionScenario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapResourceDistributionScenario_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapResourceHabitat",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: true),
                    InitialQuantityMin = table.Column<long>(type: "bigint", nullable: true),
                    InitialQuantityMax = table.Column<long>(type: "bigint", nullable: true),
                    MaximumQuantity = table.Column<long>(type: "bigint", nullable: true),
                    GrowthPercentage = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SpreadLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SpreadLimitMin = table.Column<int>(type: "int", nullable: true),
                    SpreadLimitMax = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceHabitat", x => x.Id);
                    table.CheckConstraint("CK_MapResourceHabitat_InitialQuantityRange", "[InitialQuantityMin] IS NULL OR ([InitialQuantityMin] >= 0 AND [InitialQuantityMax] >= [InitialQuantityMin])");
                    table.CheckConstraint("CK_MapResourceHabitat_SpreadLimitRange", "[SpreadLimitMin] IS NULL OR ([SpreadLimitMin] >= 0 AND [SpreadLimitMax] >= [SpreadLimitMin])");
                    table.ForeignKey(
                        name: "FK_MapResourceHabitat_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_MapResourceHabitat_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_MapResourceHabitat_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapResourceTypeLayer",
                schema: "map",
                columns: table => new
                {
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    LayerNumber = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    EmptyLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityMin = table.Column<long>(type: "bigint", nullable: false),
                    QuantityMax = table.Column<long>(type: "bigint", nullable: false),
                    DiscoveryLikelihood = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    RequiredMineLevel = table.Column<int>(type: "int", nullable: true),
                    EstimationFuzzyFactorMin = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EstimationFuzzyFactorMax = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceTypeLayer", x => new { x.MapResourceTypeId, x.LayerNumber });
                    table.CheckConstraint("CK_MapResourceTypeLayer_FuzzyFactorRange", "[EstimationFuzzyFactorMin] >= 0 AND [EstimationFuzzyFactorMax] >= [EstimationFuzzyFactorMin]");
                    table.CheckConstraint("CK_MapResourceTypeLayer_LayerNumberPositive", "[LayerNumber] >= 1");
                    table.CheckConstraint("CK_MapResourceTypeLayer_QuantityRange", "[QuantityMin] >= 0 AND [QuantityMax] >= [QuantityMin]");
                    table.ForeignKey(
                        name: "FK_MapResourceTypeLayer_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapResourceTypeLayer_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapResourceTypeYield",
                schema: "map",
                columns: table => new
                {
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    AmountPerUnit = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabledByDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceTypeYield", x => new { x.MapResourceTypeId, x.ResourceId });
                    table.CheckConstraint("CK_MapResourceTypeYield_AmountPositive", "[AmountPerUnit] > 0");
                    table.ForeignKey(
                        name: "FK_MapResourceTypeYield_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapResourceTypeYield_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerMapResourceModifier",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: true),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: true),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMapResourceModifier", x => x.Id);
                    table.CheckConstraint("CK_PlayerMapResourceModifier_HasValue", "[FlatValue] <> 0 OR [PercentValue] <> 0");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceModifier_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceModifier_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceModifier_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceModifier_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceModifier_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerMapResourceSwitch",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: true),
                    LayerNumber = table.Column<int>(type: "int", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMapResourceSwitch", x => x.Id);
                    table.CheckConstraint("CK_PlayerMapResourceSwitch_KindTarget", "([Kind] = 1 AND [ResourceId] IS NULL AND [LayerNumber] IS NULL) OR ([Kind] = 2 AND [ResourceId] IS NOT NULL AND [LayerNumber] IS NULL) OR ([Kind] = 3 AND [ResourceId] IS NULL AND [LayerNumber] >= 1)");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceSwitch_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceSwitch_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceSwitch_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceSwitch_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerMapResourceTypeKnowledge",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenOnRound = table.Column<int>(type: "int", nullable: false),
                    ResearchedOnRound = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMapResourceTypeKnowledge", x => new { x.PlayerId, x.MapResourceTypeId });
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceTypeKnowledge_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceTypeKnowledge_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerMapResourceTypeKnowledge_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CampaignMapGalleryResource",
                schema: "mapgallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapGalleryId = table.Column<int>(type: "int", nullable: false),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<byte>(type: "tinyint", nullable: false),
                    InitialQuantity = table.Column<long>(type: "bigint", nullable: true),
                    MapResourceDistributionScenarioId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapGalleryResource", x => x.Id);
                    table.CheckConstraint("CK_CampaignMapGalleryResource_InitialQuantityNonNegative", "[InitialQuantity] IS NULL OR [InitialQuantity] >= 0");
                    table.CheckConstraint("CK_CampaignMapGalleryResource_Slot", "[Slot] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_CampaignMapGalleryResource_CampaignMapGallery_CampaignMapGalleryId",
                        column: x => x.CampaignMapGalleryId,
                        principalSchema: "mapgallery",
                        principalTable: "CampaignMapGallery",
                        principalColumn: "CampaignMapGalleryId");
                    table.ForeignKey(
                        name: "FK_CampaignMapGalleryResource_MapResourceDistributionScenario_ScenarioId",
                        column: x => x.MapResourceDistributionScenarioId,
                        principalSchema: "map",
                        principalTable: "MapResourceDistributionScenario",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMapGalleryResource_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapResourceDistributionScenarioAmount",
                schema: "map",
                columns: table => new
                {
                    MapResourceDistributionScenarioId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceDistributionScenarioAmount", x => new { x.MapResourceDistributionScenarioId, x.ResourceId });
                    table.CheckConstraint("CK_MapResourceDistributionScenarioAmount_AmountPositive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_MapResourceDistributionScenarioAmount_MapResourceDistributionScenario_ScenarioId",
                        column: x => x.MapResourceDistributionScenarioId,
                        principalSchema: "map",
                        principalTable: "MapResourceDistributionScenario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapResourceDistributionScenarioAmount_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapResourceDistributionScenarioBiomeWeight",
                schema: "map",
                columns: table => new
                {
                    MapResourceDistributionScenarioId = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceDistributionScenarioBiomeWeight", x => new { x.MapResourceDistributionScenarioId, x.BiomeTypeId });
                    table.CheckConstraint("CK_MapResourceDistributionScenarioBiomeWeight_WeightPositive", "[Weight] > 0");
                    table.ForeignKey(
                        name: "FK_MapResourceDistributionScenarioBiomeWeight_CampaignBiome_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_MapResourceDistributionScenarioBiomeWeight_MapResourceDistributionScenario_ScenarioId",
                        column: x => x.MapResourceDistributionScenarioId,
                        principalSchema: "map",
                        principalTable: "MapResourceDistributionScenario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapResourceInstance",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    MapResourceTypeId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: true),
                    SpreadLimit = table.Column<int>(type: "int", nullable: true),
                    DormantSinceRound = table.Column<int>(type: "int", nullable: true),
                    EstimationFuzzyFactor = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    MapResourceDistributionScenarioId = table.Column<int>(type: "int", nullable: true),
                    CreatedOnRound = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceInstance", x => x.Id);
                    table.CheckConstraint("CK_MapResourceInstance_QuantityNonNegative", "[Quantity] IS NULL OR [Quantity] >= 0");
                    table.CheckConstraint("CK_MapResourceInstance_Slot", "[Slot] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_MapResourceInstance_SpreadLimitNonNegative", "[SpreadLimit] IS NULL OR [SpreadLimit] >= 0");
                    table.ForeignKey(
                        name: "FK_MapResourceInstance_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_MapResourceInstance_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstance_MapResourceDistributionScenario_ScenarioId",
                        column: x => x.MapResourceDistributionScenarioId,
                        principalSchema: "map",
                        principalTable: "MapResourceDistributionScenario",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstance_MapResourceType_MapResourceTypeId",
                        column: x => x.MapResourceTypeId,
                        principalSchema: "map",
                        principalTable: "MapResourceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapResourceInstanceAmount",
                schema: "game",
                columns: table => new
                {
                    MapResourceInstanceId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceInstanceAmount", x => new { x.MapResourceInstanceId, x.ResourceId });
                    table.CheckConstraint("CK_MapResourceInstanceAmount_AmountNonNegative", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceAmount_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceAmount_MapResourceInstance_MapResourceInstanceId",
                        column: x => x.MapResourceInstanceId,
                        principalSchema: "game",
                        principalTable: "MapResourceInstance",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceAmount_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapResourceInstanceLayer",
                schema: "game",
                columns: table => new
                {
                    MapResourceInstanceId = table.Column<int>(type: "int", nullable: false),
                    LayerNumber = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    EstimationFuzzyFactor = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResourceInstanceLayer", x => new { x.MapResourceInstanceId, x.LayerNumber });
                    table.CheckConstraint("CK_MapResourceInstanceLayer_LayerNumberPositive", "[LayerNumber] >= 1");
                    table.CheckConstraint("CK_MapResourceInstanceLayer_Quantity", "[Quantity] >= 0 AND ([ResourceId] IS NOT NULL OR [Quantity] = 0)");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceLayer_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceLayer_MapResourceInstance_MapResourceInstanceId",
                        column: x => x.MapResourceInstanceId,
                        principalSchema: "game",
                        principalTable: "MapResourceInstance",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapResourceInstanceLayer_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerKnownMapResource",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MapResourceInstanceId = table.Column<int>(type: "int", nullable: false),
                    DiscoveredOnRound = table.Column<int>(type: "int", nullable: false),
                    DeepestLayerSurveyed = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerKnownMapResource", x => x.Id);
                    table.CheckConstraint("CK_PlayerKnownMapResource_DeepestLayerSurveyedPositive", "[DeepestLayerSurveyed] IS NULL OR [DeepestLayerSurveyed] >= 1");
                    table.ForeignKey(
                        name: "FK_PlayerKnownMapResource_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownMapResource_MapResourceInstance_MapResourceInstanceId",
                        column: x => x.MapResourceInstanceId,
                        principalSchema: "game",
                        principalTable: "MapResourceInstance",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownMapResource_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGalleryResource_MapResourceDistributionScenarioId",
                schema: "mapgallery",
                table: "CampaignMapGalleryResource",
                column: "MapResourceDistributionScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGalleryResource_MapResourceTypeId",
                schema: "mapgallery",
                table: "CampaignMapGalleryResource",
                column: "MapResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMapGalleryResource_Tile_Slot",
                schema: "mapgallery",
                table: "CampaignMapGalleryResource",
                columns: new[] { "CampaignMapGalleryId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMapGalleryResource_Tile_Type",
                schema: "mapgallery",
                table: "CampaignMapGalleryResource",
                columns: new[] { "CampaignMapGalleryId", "MapResourceTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceCategory_Code",
                schema: "map",
                table: "MapResourceCategory",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceDistributionScenario_Type_ScenarioNumber",
                schema: "map",
                table: "MapResourceDistributionScenario",
                columns: new[] { "MapResourceTypeId", "ScenarioNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceDistributionScenarioAmount_ResourceId",
                schema: "map",
                table: "MapResourceDistributionScenarioAmount",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceDistributionScenarioBiomeWeight_BiomeTypeId",
                schema: "map",
                table: "MapResourceDistributionScenarioBiomeWeight",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceHabitat_BiomeTypeId",
                schema: "map",
                table: "MapResourceHabitat",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceHabitat_CampaignMapGeographyTypeId",
                schema: "map",
                table: "MapResourceHabitat",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceHabitat_Type_Biome_Geography",
                schema: "map",
                table: "MapResourceHabitat",
                columns: new[] { "MapResourceTypeId", "BiomeTypeId", "CampaignMapGeographyTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstance_GameId",
                schema: "game",
                table: "MapResourceInstance",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstance_MapResourceDistributionScenarioId",
                schema: "game",
                table: "MapResourceInstance",
                column: "MapResourceDistributionScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstance_MapResourceTypeId",
                schema: "game",
                table: "MapResourceInstance",
                column: "MapResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceInstance_Tile_Slot",
                schema: "game",
                table: "MapResourceInstance",
                columns: new[] { "CampaignMapId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceInstance_Tile_Type",
                schema: "game",
                table: "MapResourceInstance",
                columns: new[] { "CampaignMapId", "MapResourceTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstanceAmount_GameId",
                schema: "game",
                table: "MapResourceInstanceAmount",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstanceAmount_ResourceId",
                schema: "game",
                table: "MapResourceInstanceAmount",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstanceLayer_GameId",
                schema: "game",
                table: "MapResourceInstanceLayer",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceInstanceLayer_ResourceId",
                schema: "game",
                table: "MapResourceInstanceLayer",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceType_Category_SortOrder",
                schema: "map",
                table: "MapResourceType",
                columns: new[] { "MapResourceCategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_MapResourceType_Code",
                schema: "map",
                table: "MapResourceType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceTypeLayer_ResourceId",
                schema: "map",
                table: "MapResourceTypeLayer",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MapResourceTypeYield_ResourceId",
                schema: "map",
                table: "MapResourceTypeYield",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownMapResource_GameId",
                schema: "game",
                table: "PlayerKnownMapResource",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownMapResource_Instance",
                schema: "game",
                table: "PlayerKnownMapResource",
                column: "MapResourceInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerKnownMapResource_Player_Instance",
                schema: "game",
                table: "PlayerKnownMapResource",
                columns: new[] { "PlayerId", "MapResourceInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceModifier_BiomeTypeId",
                schema: "game",
                table: "PlayerMapResourceModifier",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceModifier_CampaignMapGeographyTypeId",
                schema: "game",
                table: "PlayerMapResourceModifier",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceModifier_GameId",
                schema: "game",
                table: "PlayerMapResourceModifier",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceModifier_MapResourceTypeId",
                schema: "game",
                table: "PlayerMapResourceModifier",
                column: "MapResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerMapResourceModifier_Player_Source_Type_Target_Biome_Geography",
                schema: "game",
                table: "PlayerMapResourceModifier",
                columns: new[] { "PlayerId", "SourceType", "SourceId", "MapResourceTypeId", "Target", "BiomeTypeId", "CampaignMapGeographyTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceSwitch_GameId",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceSwitch_MapResourceTypeId",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                column: "MapResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceSwitch_ResourceId",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Resource_Layer",
                schema: "game",
                table: "PlayerMapResourceSwitch",
                columns: new[] { "PlayerId", "SourceType", "SourceId", "Kind", "MapResourceTypeId", "ResourceId", "LayerNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceTypeKnowledge_GameId",
                schema: "game",
                table: "PlayerMapResourceTypeKnowledge",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMapResourceTypeKnowledge_MapResourceTypeId",
                schema: "game",
                table: "PlayerMapResourceTypeKnowledge",
                column: "MapResourceTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMapGalleryResource",
                schema: "mapgallery");

            migrationBuilder.DropTable(
                name: "MapResourceDistributionScenarioAmount",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceDistributionScenarioBiomeWeight",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceHabitat",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceInstanceAmount",
                schema: "game");

            migrationBuilder.DropTable(
                name: "MapResourceInstanceLayer",
                schema: "game");

            migrationBuilder.DropTable(
                name: "MapResourceTypeLayer",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceTypeYield",
                schema: "map");

            migrationBuilder.DropTable(
                name: "PlayerKnownMapResource",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerMapResourceModifier",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerMapResourceSwitch",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerMapResourceTypeKnowledge",
                schema: "game");

            migrationBuilder.DropTable(
                name: "MapResourceInstance",
                schema: "game");

            migrationBuilder.DropTable(
                name: "MapResourceDistributionScenario",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceType",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapResourceCategory",
                schema: "map");
        }
    }
}
