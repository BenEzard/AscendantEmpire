using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class T1_AddTechnologySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "technology");

            migrationBuilder.CreateTable(
                name: "ExperienceType",
                schema: "technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameConfiguration",
                schema: "game",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false),
                    EmpireBaseResearchPoints = table.Column<int>(type: "int", nullable: false),
                    TechnologySpeedMultiplierPercent = table.Column<long>(type: "bigint", nullable: false),
                    TechnologyFuzzinessPercent = table.Column<long>(type: "bigint", nullable: false),
                    TechnologyCustomisation = table.Column<int>(type: "int", nullable: false),
                    ResearchOverflowEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UnspentResearchPointsBehaviour = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameConfiguration", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameConfiguration_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerResearchModifier",
                schema: "technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Bucket = table.Column<int>(type: "int", nullable: false),
                    FlatValue = table.Column<long>(type: "bigint", nullable: false),
                    PercentValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerResearchModifier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerResearchModifier_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResearchHistory",
                schema: "technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    PlayerTurnId = table.Column<int>(type: "int", nullable: true),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    Bucket = table.Column<int>(type: "int", nullable: false),
                    Generated = table.Column<int>(type: "int", nullable: false),
                    Spent = table.Column<int>(type: "int", nullable: false),
                    Overflowed = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchHistory_PlayerTurn_PlayerTurnId",
                        column: x => x.PlayerTurnId,
                        principalSchema: "turns",
                        principalTable: "PlayerTurn",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResearchHistory_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Technology",
                schema: "technology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IconPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Era = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TreeColumn = table.Column<int>(type: "int", nullable: true),
                    TreeRow = table.Column<int>(type: "int", nullable: true),
                    DirectedRequired = table.Column<int>(type: "int", nullable: true),
                    ExperienceRequired = table.Column<int>(type: "int", nullable: true),
                    DiffusionRequired = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technology", x => x.Id);
                    table.CheckConstraint("CK_Technology_AtLeastOneBucket", "[DirectedRequired] IS NOT NULL OR [ExperienceRequired] IS NOT NULL OR [DiffusionRequired] IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "PendingExperienceAccrual",
                schema: "technology",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ExperienceTypeId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingExperienceAccrual", x => new { x.PlayerId, x.ExperienceTypeId });
                    table.CheckConstraint("CK_PendingExperienceAccrual_PointsNonNegative", "[Points] >= 0");
                    table.ForeignKey(
                        name: "FK_PendingExperienceAccrual_ExperienceType_ExperienceTypeId",
                        column: x => x.ExperienceTypeId,
                        principalSchema: "technology",
                        principalTable: "ExperienceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingExperienceAccrual_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnusedExperience",
                schema: "technology",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ExperienceTypeId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnusedExperience", x => new { x.PlayerId, x.ExperienceTypeId });
                    table.CheckConstraint("CK_PlayerUnusedExperience_PointsNonNegative", "[Points] >= 0");
                    table.ForeignKey(
                        name: "FK_PlayerUnusedExperience_ExperienceType_ExperienceTypeId",
                        column: x => x.ExperienceTypeId,
                        principalSchema: "technology",
                        principalTable: "ExperienceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnusedExperience_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerResearchState",
                schema: "technology",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    CurrentTechnologyId = table.Column<int>(type: "int", nullable: true),
                    NextTechnologyId = table.Column<int>(type: "int", nullable: true),
                    UnspentDirectedPoints = table.Column<int>(type: "int", nullable: false),
                    UnspentDiffusionPoints = table.Column<int>(type: "int", nullable: false),
                    LastResearchAppliedOnRound = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerResearchState", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_PlayerResearchState_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerResearchState_Technology_CurrentTechnologyId",
                        column: x => x.CurrentTechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerResearchState_Technology_NextTechnologyId",
                        column: x => x.NextTechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerTechnology",
                schema: "technology",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DirectedRequired = table.Column<int>(type: "int", nullable: true),
                    ExperienceRequired = table.Column<int>(type: "int", nullable: true),
                    DiffusionRequired = table.Column<int>(type: "int", nullable: true),
                    DirectedPoints = table.Column<int>(type: "int", nullable: false),
                    ExperiencePoints = table.Column<int>(type: "int", nullable: false),
                    DiffusionPoints = table.Column<int>(type: "int", nullable: false),
                    AcquisitionPaidOnRound = table.Column<int>(type: "int", nullable: true),
                    DiscoveredOnRound = table.Column<int>(type: "int", nullable: true),
                    DiscoveryMethod = table.Column<int>(type: "int", nullable: true),
                    DiscoveredFromPlayerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTechnology", x => new { x.PlayerId, x.TechnologyId });
                    table.CheckConstraint("CK_PlayerTechnology_AtLeastOneBucket", "[DirectedRequired] IS NOT NULL OR [ExperienceRequired] IS NOT NULL OR [DiffusionRequired] IS NOT NULL");
                    table.CheckConstraint("CK_PlayerTechnology_PointsNonNegative", "[DirectedPoints] >= 0 AND [ExperiencePoints] >= 0 AND [DiffusionPoints] >= 0");
                    table.CheckConstraint("CK_PlayerTechnology_StatusStamps", "([Status] IN (1, 2) AND [AcquisitionPaidOnRound] IS NULL AND [DiscoveredOnRound] IS NULL) OR ([Status] = 3 AND [AcquisitionPaidOnRound] IS NOT NULL AND [DiscoveredOnRound] IS NULL) OR ([Status] = 4 AND [DiscoveredOnRound] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlayerTechnology_Player_DiscoveredFromPlayerId",
                        column: x => x.DiscoveredFromPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerTechnology_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerTechnology_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnologyAcquisitionCost",
                schema: "technology",
                columns: table => new
                {
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyAcquisitionCost", x => new { x.TechnologyId, x.AssetId });
                    table.CheckConstraint("CK_TechnologyAcquisitionCost_QuantityPositive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_TechnologyAcquisitionCost_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "assets",
                        principalTable: "Asset",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnologyAcquisitionCost_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnologyExperienceType",
                schema: "technology",
                columns: table => new
                {
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    ExperienceTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyExperienceType", x => new { x.TechnologyId, x.ExperienceTypeId });
                    table.ForeignKey(
                        name: "FK_TechnologyExperienceType_ExperienceType_ExperienceTypeId",
                        column: x => x.ExperienceTypeId,
                        principalSchema: "technology",
                        principalTable: "ExperienceType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnologyExperienceType_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnologyGrant",
                schema: "technology",
                columns: table => new
                {
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    GrantEffect = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyGrant", x => new { x.TechnologyId, x.TargetType, x.TargetId });
                    table.ForeignKey(
                        name: "FK_TechnologyGrant_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TechnologyPrerequisite",
                schema: "technology",
                columns: table => new
                {
                    TechnologyId = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteTechnologyId = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteKind = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyPrerequisite", x => new { x.TechnologyId, x.PrerequisiteTechnologyId });
                    table.CheckConstraint("CK_TechnologyPrerequisite_NotSelf", "[TechnologyId] <> [PrerequisiteTechnologyId]");
                    table.ForeignKey(
                        name: "FK_TechnologyPrerequisite_Technology_PrerequisiteTechnologyId",
                        column: x => x.PrerequisiteTechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnologyPrerequisite_Technology_TechnologyId",
                        column: x => x.TechnologyId,
                        principalSchema: "technology",
                        principalTable: "Technology",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_ExperienceType_Code",
                schema: "technology",
                table: "ExperienceType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExperienceType_DisplayName",
                schema: "technology",
                table: "ExperienceType",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExperienceType_SortOrder",
                schema: "technology",
                table: "ExperienceType",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingExperienceAccrual_ExperienceType",
                schema: "technology",
                table: "PendingExperienceAccrual",
                column: "ExperienceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerResearchModifier_Player_Source_Bucket",
                schema: "technology",
                table: "PlayerResearchModifier",
                columns: new[] { "PlayerId", "SourceType", "SourceId", "Bucket" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerResearchState_CurrentTechnologyId",
                schema: "technology",
                table: "PlayerResearchState",
                column: "CurrentTechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerResearchState_NextTechnologyId",
                schema: "technology",
                table: "PlayerResearchState",
                column: "NextTechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTechnology_DiscoveredFromPlayerId",
                schema: "technology",
                table: "PlayerTechnology",
                column: "DiscoveredFromPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTechnology_Game_Technology_Status",
                schema: "technology",
                table: "PlayerTechnology",
                columns: new[] { "GameId", "TechnologyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTechnology_Player_Status",
                schema: "technology",
                table: "PlayerTechnology",
                columns: new[] { "PlayerId", "Status" })
                .Annotation("SqlServer:Include", new[] { "DirectedPoints", "ExperiencePoints", "DiffusionPoints" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTechnology_TechnologyId",
                schema: "technology",
                table: "PlayerTechnology",
                column: "TechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnusedExperience_ExperienceTypeId",
                schema: "technology",
                table: "PlayerUnusedExperience",
                column: "ExperienceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnusedExperience_Game_ExperienceType",
                schema: "technology",
                table: "PlayerUnusedExperience",
                columns: new[] { "GameId", "ExperienceTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchHistory_Player_Round",
                schema: "technology",
                table: "ResearchHistory",
                columns: new[] { "PlayerId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchHistory_PlayerTurnId",
                schema: "technology",
                table: "ResearchHistory",
                column: "PlayerTurnId");

            migrationBuilder.CreateIndex(
                name: "UX_Technology_Code",
                schema: "technology",
                table: "Technology",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Technology_DisplayName",
                schema: "technology",
                table: "Technology",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Technology_Era_SortOrder",
                schema: "technology",
                table: "Technology",
                columns: new[] { "Era", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyAcquisitionCost_Asset",
                schema: "technology",
                table: "TechnologyAcquisitionCost",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyExperienceType_ExperienceType",
                schema: "technology",
                table: "TechnologyExperienceType",
                column: "ExperienceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyGrant_Target",
                schema: "technology",
                table: "TechnologyGrant",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyPrerequisite_Prerequisite",
                schema: "technology",
                table: "TechnologyPrerequisite",
                column: "PrerequisiteTechnologyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameConfiguration",
                schema: "game");

            migrationBuilder.DropTable(
                name: "PendingExperienceAccrual",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "PlayerResearchModifier",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "PlayerResearchState",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "PlayerTechnology",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "PlayerUnusedExperience",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "ResearchHistory",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "TechnologyAcquisitionCost",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "TechnologyExperienceType",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "TechnologyGrant",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "TechnologyPrerequisite",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "ExperienceType",
                schema: "technology");

            migrationBuilder.DropTable(
                name: "Technology",
                schema: "technology");
        }
    }
}
