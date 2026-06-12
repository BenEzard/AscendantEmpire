using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.EnsureSchema(
                name: "battle");

            migrationBuilder.EnsureSchema(
                name: "map");

            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.EnsureSchema(
                name: "mapgallery");

            migrationBuilder.EnsureSchema(
                name: "unitdesigns");

            migrationBuilder.EnsureSchema(
                name: "util");

            migrationBuilder.EnsureSchema(
                name: "resources");

            migrationBuilder.EnsureSchema(
                name: "structure");

            migrationBuilder.EnsureSchema(
                name: "units");

            migrationBuilder.EnsureSchema(
                name: "troops");

            migrationBuilder.EnsureSchema(
                name: "turns");

            migrationBuilder.CreateTable(
                name: "BackgroundProcessing",
                schema: "system",
                columns: table => new
                {
                    ProcessingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProgressPercent = table.Column<byte>(type: "tinyint", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundProcessing", x => x.ProcessingId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignBiomes",
                schema: "map",
                columns: table => new
                {
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DoesBlockSight = table.Column<bool>(type: "bit", nullable: false),
                    IsWater = table.Column<bool>(type: "bit", nullable: false),
                    IsTraversable = table.Column<bool>(type: "bit", nullable: false),
                    RegenerationRate = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    IsPaintableOnCampaignMap = table.Column<bool>(type: "bit", nullable: false),
                    MapEditorHtmlColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CanBeElevated = table.Column<bool>(type: "bit", nullable: false),
                    BaseForageValue = table.Column<int>(type: "int", nullable: true),
                    BaseHuntValue = table.Column<int>(type: "int", nullable: true),
                    BaseFishValue = table.Column<int>(type: "int", nullable: true),
                    BaseFodderValue = table.Column<int>(type: "int", nullable: true),
                    BaseBoneValue = table.Column<int>(type: "int", nullable: true),
                    BaseWoodValue = table.Column<int>(type: "int", nullable: true),
                    BaseStoneValue = table.Column<int>(type: "int", nullable: true),
                    BaseSpecialValue = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignBiomes", x => x.BiomeTypeId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignGeography",
                schema: "map",
                columns: table => new
                {
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelativeElevation = table.Column<int>(type: "int", nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MapEditorIcon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsPaintableOnCampaignMap = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignGeography", x => x.CampaignMapGeographyTypeId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMapGalleryIndex",
                schema: "mapgallery",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CreationStatus = table.Column<int>(type: "int", nullable: false),
                    ProgressPercent = table.Column<byte>(type: "tinyint", nullable: false),
                    CreationEngine = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CampaignMapOffsetId = table.Column<int>(type: "int", nullable: false),
                    HorizontalWrap = table.Column<bool>(type: "bit", nullable: false),
                    VerticalWrap = table.Column<bool>(type: "bit", nullable: false),
                    GeographyMapPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BiomeMapPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapGalleryIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameIndex",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapIndexId = table.Column<int>(type: "int", nullable: false),
                    GameName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NumberOfPlayers = table.Column<int>(type: "int", nullable: false),
                    CreationStatus = table.Column<int>(type: "int", nullable: false),
                    PlayStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GenerationSeed = table.Column<long>(type: "bigint", nullable: false),
                    BattleMapSize = table.Column<int>(type: "int", nullable: false),
                    HorizontalWrap = table.Column<bool>(type: "bit", nullable: false),
                    VerticalWrap = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameIndex", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Numbers",
                schema: "util",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Numbers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceMetaCategory",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMetaCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Structures",
                schema: "structure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordTypeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresSlotType = table.Column<int>(type: "int", nullable: false),
                    AllowedPositions = table.Column<int>(type: "int", nullable: false),
                    CanBeBuiltOnCampaignMap = table.Column<bool>(type: "bit", nullable: false),
                    CanBeBuiltOnBattleMap = table.Column<bool>(type: "bit", nullable: false),
                    DualMapBuildTrigger = table.Column<bool>(type: "bit", nullable: false),
                    SightRange = table.Column<int>(type: "int", nullable: false),
                    SightDirection = table.Column<int>(type: "int", nullable: false),
                    MapImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Structures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMapGallery",
                schema: "mapgallery",
                columns: table => new
                {
                    CampaignMapGalleryId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGalleryIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapGallery", x => x.CampaignMapGalleryId);
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMapGallery_CampaignMapGalleryIndex_CampaignMapGalleryIndexId",
                        column: x => x.CampaignMapGalleryIndexId,
                        principalSchema: "mapgallery",
                        principalTable: "CampaignMapGalleryIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Player",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    RulerTitle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RulerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FactionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CampaignOffsetId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Player", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Player_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceCategory",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceMetaCategoryId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryType = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceCategory_ResourceMetaCategory_ResourceMetaCategoryId",
                        column: x => x.ResourceMetaCategoryId,
                        principalSchema: "resources",
                        principalTable: "ResourceMetaCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Battle",
                schema: "battle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    GlobalBattleName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CommencedAtRoundNumber = table.Column<int>(type: "int", nullable: false),
                    OriginalTileStatus = table.Column<int>(type: "int", nullable: false),
                    OriginalTileOwnerId = table.Column<int>(type: "int", nullable: true),
                    ConcludedAtRoundNumber = table.Column<int>(type: "int", nullable: true),
                    VictorId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Battle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Battle_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Battle_Player_OriginalTileOwnerId",
                        column: x => x.OriginalTileOwnerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Battle_Player_VictorId",
                        column: x => x.VictorId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CampaignMap",
                schema: "game",
                columns: table => new
                {
                    CampaignMapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    ForageAmount = table.Column<int>(type: "int", nullable: true),
                    HuntAmount = table.Column<int>(type: "int", nullable: true),
                    FishAmount = table.Column<int>(type: "int", nullable: true),
                    FodderAmount = table.Column<int>(type: "int", nullable: true),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenByPlayerId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedByPlayerId = table.Column<int>(type: "int", nullable: true),
                    ControlledByPlayerId = table.Column<int>(type: "int", nullable: true),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMap", x => x.CampaignMapId);
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_ControlledByPlayerId",
                        column: x => x.ControlledByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_FirstSeenByPlayerId",
                        column: x => x.FirstSeenByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_LastVisitedByPlayerId",
                        column: x => x.LastVisitedByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UnitDesigns",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    DesignName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    MapImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitDesigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitDesigns_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitDesigns_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceGroup",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceCategoryId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceGroup_ResourceCategory_ResourceCategoryId",
                        column: x => x.ResourceCategoryId,
                        principalSchema: "resources",
                        principalTable: "ResourceCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BattleParticipant",
                schema: "battle",
                columns: table => new
                {
                    BattleId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    JoinedAtRoundNumber = table.Column<int>(type: "int", nullable: false),
                    IsStillInBattle = table.Column<bool>(type: "bit", nullable: false),
                    LeftAtRoundNumber = table.Column<int>(type: "int", nullable: true),
                    ExitReason = table.Column<int>(type: "int", nullable: false),
                    PlayerBattleName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    WasSupplied = table.Column<bool>(type: "bit", nullable: false),
                    IsSupplied = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleParticipant", x => new { x.BattleId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_BattleParticipant_Battle_BattleId",
                        column: x => x.BattleId,
                        principalSchema: "battle",
                        principalTable: "Battle",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BattleParticipant_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TurnQueue",
                schema: "turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    QueueType = table.Column<int>(type: "int", nullable: false),
                    BattleId = table.Column<int>(type: "int", nullable: true),
                    CurrentRoundNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentTurnStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentScheduledTurnEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentActualTurnEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnQueue_Battle_BattleId",
                        column: x => x.BattleId,
                        principalSchema: "battle",
                        principalTable: "Battle",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnQueue_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BattleMapIndex",
                schema: "battle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    BattleMapOffsetId = table.Column<int>(type: "int", nullable: false),
                    GenerationStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMapIndex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleMapIndex_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerCampaignMap",
                schema: "game",
                columns: table => new
                {
                    PlayerCampaignMapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapGeographyTypeId = table.Column<int>(type: "int", nullable: false),
                    FirstSeenOnTurnId = table.Column<int>(type: "int", nullable: true),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCampaignMap", x => x.PlayerCampaignMapId);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignGeography_CampaignMapGeographyTypeId",
                        column: x => x.CampaignMapGeographyTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignGeography",
                        principalColumn: "CampaignMapGeographyTypeId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StructureInstances",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    BattleMapTileId = table.Column<int>(type: "int", nullable: true),
                    StructureId = table.Column<int>(type: "int", nullable: false),
                    SlotType = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    OwnedByPlayerId = table.Column<int>(type: "int", nullable: false),
                    CreatedOnTurnId = table.Column<int>(type: "int", nullable: false),
                    CompletionPercent = table.Column<double>(type: "float", nullable: false),
                    CompletedOnTurnId = table.Column<int>(type: "int", nullable: true),
                    RemovedOnTurnId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureInstances_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_StructureInstances_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureInstances_Player_OwnedByPlayerId",
                        column: x => x.OwnedByPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StructureInstances_Structures_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "structure",
                        principalTable: "Structures",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitDesignId = table.Column<int>(type: "int", nullable: false),
                    SightRange = table.Column<int>(type: "int", nullable: false),
                    MovementPoints = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ModifiersJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledDesignStats", x => x.UnitDesignId);
                    table.ForeignKey(
                        name: "FK_CompiledDesignStats_UnitDesigns_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitDesigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnitDesignStates",
                schema: "unitdesigns",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitDesignId = table.Column<int>(type: "int", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    HasBeenPrototyped = table.Column<bool>(type: "bit", nullable: false),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnitDesignStates", x => new { x.PlayerId, x.UnitDesignId });
                    table.ForeignKey(
                        name: "FK_PlayerUnitDesignStates_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnitDesignStates_UnitDesigns_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitDesigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopInstances",
                schema: "units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    TroopName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BattleMapTileId = table.Column<int>(type: "int", nullable: true),
                    UnitDesignId = table.Column<int>(type: "int", nullable: false),
                    InitialStrength = table.Column<int>(type: "int", nullable: false),
                    InjuredCount = table.Column<int>(type: "int", nullable: false),
                    HealthyCount = table.Column<int>(type: "int", nullable: false),
                    MovementPointsRemaining = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopInstances_CampaignMap_CampaignMapTileId",
                        column: x => x.CampaignMapTileId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_TroopInstances_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopInstances_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopInstances_UnitDesigns_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitDesigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Resource",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsGradedResource = table.Column<bool>(type: "bit", nullable: false),
                    ResourceGrade = table.Column<int>(type: "int", nullable: false),
                    ResourceMetaCategoryId = table.Column<int>(type: "int", nullable: false),
                    ResourceGroupId = table.Column<int>(type: "int", nullable: true),
                    ResourceCategoryId = table.Column<int>(type: "int", nullable: true),
                    NeverLuxury = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource", x => x.Id);
                    table.CheckConstraint("CK_Resource_GroupOrCategoryXor", "([ResourceGroupId] IS NOT NULL AND [ResourceCategoryId] IS NULL) OR ([ResourceGroupId] IS NULL AND [ResourceCategoryId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Resource_ResourceCategory_ResourceCategoryId",
                        column: x => x.ResourceCategoryId,
                        principalSchema: "resources",
                        principalTable: "ResourceCategory",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Resource_ResourceGroup_ResourceGroupId",
                        column: x => x.ResourceGroupId,
                        principalSchema: "resources",
                        principalTable: "ResourceGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Resource_ResourceMetaCategory_ResourceMetaCategoryId",
                        column: x => x.ResourceMetaCategoryId,
                        principalSchema: "resources",
                        principalTable: "ResourceMetaCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnQueueParticipant",
                schema: "turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnQueueId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TurnOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsActiveInLane = table.Column<bool>(type: "bit", nullable: false),
                    LastCompletedRoundNumber = table.Column<int>(type: "int", nullable: true),
                    LastTurnStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTurnEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnQueueParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnQueueParticipant_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnQueueParticipant_TurnQueue_TurnQueueId",
                        column: x => x.TurnQueueId,
                        principalSchema: "turns",
                        principalTable: "TurnQueue",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BattleMap",
                schema: "battle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    BattleMapIndexId = table.Column<int>(type: "int", nullable: false),
                    XCoord = table.Column<int>(type: "int", nullable: false),
                    YCoord = table.Column<int>(type: "int", nullable: false),
                    AltitudeLevel = table.Column<int>(type: "int", nullable: false),
                    BiomeTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleMap_BattleMapIndex_BattleMapIndexId",
                        column: x => x.BattleMapIndexId,
                        principalSchema: "battle",
                        principalTable: "BattleMapIndex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattleMap_CampaignBiomes_BiomeTypeId",
                        column: x => x.BiomeTypeId,
                        principalSchema: "map",
                        principalTable: "CampaignBiomes",
                        principalColumn: "BiomeTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerKnownStructures",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: false),
                    LastSeenOnTurnId = table.Column<int>(type: "int", nullable: false),
                    ObservedCompletionPercent = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerKnownStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerKnownStructures_StructureInstances_StructureInstanceId",
                        column: x => x.StructureInstanceId,
                        principalSchema: "game",
                        principalTable: "StructureInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopOrders",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    TroopInstanceId = table.Column<int>(type: "int", nullable: false),
                    OrderType = table.Column<int>(type: "int", nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    TargetCampaignMapTileId = table.Column<int>(type: "int", nullable: true),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    ResourceDefinitionId = table.Column<int>(type: "int", nullable: true),
                    CurrentMovementStepIndex = table.Column<int>(type: "int", nullable: true),
                    WorkRequired = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WorkCompleted = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    EffectiveFromTurnNumber = table.Column<int>(type: "int", nullable: true),
                    TradeSourceStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    TradeTargetStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    DefendTargetTroopInstanceId = table.Column<int>(type: "int", nullable: true),
                    DefendTargetStructureInstanceId = table.Column<int>(type: "int", nullable: true),
                    CreatedTurnNumber = table.Column<int>(type: "int", nullable: false),
                    LastProcessedTurnNumber = table.Column<int>(type: "int", nullable: true),
                    CompletedTurnNumber = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrders_TroopInstances_TroopInstanceId",
                        column: x => x.TroopInstanceId,
                        principalSchema: "units",
                        principalTable: "TroopInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopOrderMovementSteps",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TroopOrderId = table.Column<int>(type: "int", nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false),
                    CampaignMapTileId = table.Column<int>(type: "int", nullable: false),
                    MovementCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CompletedTurnNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrderMovementSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrderMovementSteps_TroopOrders_TroopOrderId",
                        column: x => x.TroopOrderId,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TroopOrderParticipants",
                schema: "troops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TroopOrderId = table.Column<int>(type: "int", nullable: false),
                    TroopInstanceId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    TroopOrderId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TroopOrderParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopInstances_TroopInstanceId",
                        column: x => x.TroopInstanceId,
                        principalSchema: "units",
                        principalTable: "TroopInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopOrders_TroopOrderId",
                        column: x => x.TroopOrderId,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TroopOrderParticipants_TroopOrders_TroopOrderId1",
                        column: x => x.TroopOrderId1,
                        principalSchema: "troops",
                        principalTable: "TroopOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processing_ProcessType",
                schema: "system",
                table: "BackgroundProcessing",
                column: "ProcessType");

            migrationBuilder.CreateIndex(
                name: "IX_Processing_Status",
                schema: "system",
                table: "BackgroundProcessing",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Battle_GameId",
                schema: "battle",
                table: "Battle",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Battle_OriginalTileOwnerId",
                schema: "battle",
                table: "Battle",
                column: "OriginalTileOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Battle_VictorId",
                schema: "battle",
                table: "Battle",
                column: "VictorId");

            migrationBuilder.CreateIndex(
                name: "UX_Battle_Game_Tile_Open",
                schema: "battle",
                table: "Battle",
                columns: new[] { "GameId", "CampaignMapTileId" },
                unique: true,
                filter: "[ConcludedAtRoundNumber] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BattleMap_BattleMapIndexId",
                schema: "battle",
                table: "BattleMap",
                column: "BattleMapIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleMap_BattleMapIndexId_XCoord_YCoord",
                schema: "battle",
                table: "BattleMap",
                columns: new[] { "BattleMapIndexId", "XCoord", "YCoord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattleMap_BiomeTypeId",
                schema: "battle",
                table: "BattleMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleMapIndex_CampaignMapId",
                schema: "battle",
                table: "BattleMapIndex",
                column: "CampaignMapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_BattleId_IsStillInBattle",
                schema: "battle",
                table: "BattleParticipant",
                columns: new[] { "BattleId", "IsStillInBattle" });

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_PlayerId_IsStillInBattle",
                schema: "battle",
                table: "BattleParticipant",
                columns: new[] { "PlayerId", "IsStillInBattle" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_BiomeTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "CampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_ControlledByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "ControlledByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_FirstSeenByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstSeenByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_Game_ControlledByPlayer",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "ControlledByPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_LastVisitedByPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_CampaignMap_Game_X_Y",
                schema: "game",
                table: "CampaignMap",
                columns: new[] { "GameId", "XCoord", "YCoord" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_CampaignMapGalleryIndexId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGalleryIndexId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapGallery_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Player_GameId",
                schema: "game",
                table: "Player",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_BiomeTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "BiomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapGeographyTypeId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapGeographyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_CampaignMapId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_GameId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCampaignMap_PlayerId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_CampaignMapId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "CampaignMapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_LastSeen",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: new[] { "GameId", "PlayerId", "LastSeenOnTurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_Game_Player_Tile",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: new[] { "GameId", "PlayerId", "CampaignMapId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_PlayerId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerKnownStructures_StructureInstanceId",
                schema: "game",
                table: "PlayerKnownStructures",
                column: "StructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerKnownStructures_Game_Player_Instance",
                schema: "game",
                table: "PlayerKnownStructures",
                columns: new[] { "GameId", "PlayerId", "StructureInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnitDesignStates_UnitDesignId",
                schema: "unitdesigns",
                table: "PlayerUnitDesignStates",
                column: "UnitDesignId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_Code",
                schema: "resources",
                table: "Resource",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resource_DisplayName",
                schema: "resources",
                table: "Resource",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceCategoryId",
                schema: "resources",
                table: "Resource",
                column: "ResourceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceGroupId",
                schema: "resources",
                table: "Resource",
                column: "ResourceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceMetaCategoryId",
                schema: "resources",
                table: "Resource",
                column: "ResourceMetaCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCategory_ResourceMetaCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceCategory",
                columns: new[] { "ResourceMetaCategoryId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_DisplayName",
                schema: "resources",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_ResourceCategoryId_SortOrder",
                schema: "resources",
                table: "ResourceGroup",
                columns: new[] { "ResourceCategoryId", "SortOrder" },
                unique: true,
                filter: "[SortOrder] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetaCategory_DisplayName",
                schema: "resources",
                table: "ResourceMetaCategory",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Owner_BattleMap",
                schema: "game",
                table: "StructureInstances",
                columns: new[] { "GameId", "OwnedByPlayerId", "BattleMapTileId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Owner_RemovedOn",
                schema: "game",
                table: "StructureInstances",
                columns: new[] { "GameId", "OwnedByPlayerId", "RemovedOnTurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_RemovedOn",
                schema: "game",
                table: "StructureInstances",
                columns: new[] { "GameId", "RemovedOnTurnId" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStructureInstances_Game_Tile",
                schema: "game",
                table: "StructureInstances",
                columns: new[] { "GameId", "CampaignMapTileId" });

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_CampaignMapTileId_SlotType",
                schema: "game",
                table: "StructureInstances",
                columns: new[] { "CampaignMapTileId", "SlotType" },
                unique: true,
                filter: "[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_OwnedByPlayerId",
                schema: "game",
                table: "StructureInstances",
                column: "OwnedByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureInstances_StructureId",
                schema: "game",
                table: "StructureInstances",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_CampaignStructureInstances_Active_BattleTile",
                schema: "game",
                table: "StructureInstances",
                column: "BattleMapTileId",
                unique: true,
                filter: "[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_CampaignMapTileId",
                schema: "units",
                table: "TroopInstances",
                column: "CampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_GameId",
                schema: "units",
                table: "TroopInstances",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_PlayerId",
                schema: "units",
                table: "TroopInstances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopInstances_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitDesignId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_CampaignMapTileId_IsCompleted",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                columns: new[] { "CampaignMapTileId", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_TroopOrderId",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                column: "TroopOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderMovementSteps_TroopOrderId_StepIndex",
                schema: "troops",
                table: "TroopOrderMovementSteps",
                columns: new[] { "TroopOrderId", "StepIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId_TroopInstanceId",
                schema: "troops",
                table: "TroopOrderParticipants",
                columns: new[] { "TroopOrderId", "TroopInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrderParticipants_TroopOrderId1",
                schema: "troops",
                table: "TroopOrderParticipants",
                column: "TroopOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_DefendTargetStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "DefendTargetStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_DefendTargetTroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "DefendTargetTroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId",
                schema: "troops",
                table: "TroopOrders",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_OrderType_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "OrderType", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_GameId_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "GameId", "TroopInstanceId", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_ResourceDefinitionId",
                schema: "troops",
                table: "TroopOrders",
                column: "ResourceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_StructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "StructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TargetCampaignMapTileId",
                schema: "troops",
                table: "TroopOrders",
                column: "TargetCampaignMapTileId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TradeSourceStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TradeSourceStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TradeTargetStructureInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TradeTargetStructureInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TroopInstanceId",
                schema: "troops",
                table: "TroopOrders",
                column: "TroopInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TroopOrders_TroopInstanceId_OrderStatus",
                schema: "troops",
                table: "TroopOrders",
                columns: new[] { "TroopInstanceId", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueue_GameId",
                schema: "turns",
                table: "TurnQueue",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueue_GameId_QueueType_IsCompleted",
                schema: "turns",
                table: "TurnQueue",
                columns: new[] { "GameId", "QueueType", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "UX_TurnQueue_Battle",
                schema: "turns",
                table: "TurnQueue",
                column: "BattleId",
                unique: true,
                filter: "[BattleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TurnQueue_Campaign_Game_Active",
                schema: "turns",
                table: "TurnQueue",
                columns: new[] { "GameId", "QueueType" },
                unique: true,
                filter: "[QueueType] = 0 AND [BattleId] IS NULL AND [IsCompleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_PlayerId",
                schema: "turns",
                table: "TurnQueueParticipant",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_IsActiveInLane_TurnOrder",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "IsActiveInLane", "TurnOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_PlayerId",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_Status",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnQueueParticipant_TurnQueueId_TurnOrder",
                schema: "turns",
                table: "TurnQueueParticipant",
                columns: new[] { "TurnQueueId", "TurnOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitDesigns_GameId",
                schema: "unitdesigns",
                table: "UnitDesigns",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDesigns_PlayerId",
                schema: "unitdesigns",
                table: "UnitDesigns",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundProcessing",
                schema: "system");

            migrationBuilder.DropTable(
                name: "BattleMap",
                schema: "battle");

            migrationBuilder.DropTable(
                name: "BattleParticipant",
                schema: "battle");

            migrationBuilder.DropTable(
                name: "CampaignMapGallery",
                schema: "mapgallery");

            migrationBuilder.DropTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "Numbers",
                schema: "util");

            migrationBuilder.DropTable(
                name: "PlayerCampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerKnownStructures",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PlayerUnitDesignStates",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "Resource",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "TroopOrderMovementSteps",
                schema: "troops");

            migrationBuilder.DropTable(
                name: "TroopOrderParticipants",
                schema: "troops");

            migrationBuilder.DropTable(
                name: "TurnQueueParticipant",
                schema: "turns");

            migrationBuilder.DropTable(
                name: "BattleMapIndex",
                schema: "battle");

            migrationBuilder.DropTable(
                name: "CampaignMapGalleryIndex",
                schema: "mapgallery");

            migrationBuilder.DropTable(
                name: "StructureInstances",
                schema: "game");

            migrationBuilder.DropTable(
                name: "ResourceGroup",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "TroopOrders",
                schema: "troops");

            migrationBuilder.DropTable(
                name: "TurnQueue",
                schema: "turns");

            migrationBuilder.DropTable(
                name: "Structures",
                schema: "structure");

            migrationBuilder.DropTable(
                name: "ResourceCategory",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "TroopInstances",
                schema: "units");

            migrationBuilder.DropTable(
                name: "Battle",
                schema: "battle");

            migrationBuilder.DropTable(
                name: "ResourceMetaCategory",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "CampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "UnitDesigns",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "CampaignBiomes",
                schema: "map");

            migrationBuilder.DropTable(
                name: "CampaignGeography",
                schema: "map");

            migrationBuilder.DropTable(
                name: "Player",
                schema: "game");

            migrationBuilder.DropTable(
                name: "GameIndex",
                schema: "game");
        }
    }
}
