using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS1_AddGameCampaignMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.CreateTable(
                name: "Index",
                schema: "game",
                columns: table => new
                {
                    GameIndexId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignMapIndexId = table.Column<int>(type: "int", nullable: false),
                    GameName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NumberOfPlayers = table.Column<int>(type: "int", nullable: false),
                    GameGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationStatus = table.Column<int>(type: "int", nullable: false),
                    PlayStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Index", x => x.GameIndexId);
                });

            migrationBuilder.CreateTable(
                name: "Player",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    RulerTitle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RulerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FactionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Player", x => x.PlayerId);
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
                    FirstVisitedTurnId = table.Column<int>(type: "int", nullable: true),
                    FirstVisitedPlayerId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedTurnId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedPlayerId = table.Column<int>(type: "int", nullable: true),
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
                        name: "FK_CampaignMap_Index_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "Index",
                        principalColumn: "GameIndexId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_FirstVisitedPlayerId",
                        column: x => x.FirstVisitedPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "PlayerId");
                    table.ForeignKey(
                        name: "FK_CampaignMap_Player_LastVisitedPlayerId",
                        column: x => x.LastVisitedPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "PlayerId");
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
                    FirstVisitedTurnId = table.Column<int>(type: "int", nullable: true),
                    LastVisitedTurnId = table.Column<int>(type: "int", nullable: true)
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
                        name: "FK_PlayerCampaignMap_Index_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "Index",
                        principalColumn: "GameIndexId");
                    table.ForeignKey(
                        name: "FK_PlayerCampaignMap_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_CampaignMap_FirstVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "FirstVisitedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_GameId",
                schema: "game",
                table: "CampaignMap",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_LastVisitedPlayerId",
                schema: "game",
                table: "CampaignMap",
                column: "LastVisitedPlayerId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "BiomeTypeId",
                principalSchema: "map",
                principalTable: "CampaignBiomes",
                principalColumn: "BiomeTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGeographyTypeId",
                principalSchema: "map",
                principalTable: "CampaignGeography",
                principalColumn: "CampaignMapGeographyTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery");

            migrationBuilder.DropTable(
                name: "PlayerCampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "CampaignMap",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Index",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Player",
                schema: "game");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignBiomes_BiomeTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "BiomeTypeId",
                principalSchema: "map",
                principalTable: "CampaignBiomes",
                principalColumn: "BiomeTypeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMapGallery_CampaignGeography_CampaignMapGeographyTypeId",
                schema: "mapgallery",
                table: "CampaignMapGallery",
                column: "CampaignMapGeographyTypeId",
                principalSchema: "map",
                principalTable: "CampaignGeography",
                principalColumn: "CampaignMapGeographyTypeId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
