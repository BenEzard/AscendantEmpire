using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CD1_AddSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settlement");

            migrationBuilder.AddColumn<long>(
                name: "BuildPoints",
                schema: "structure",
                table: "Structures",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "BuildPointsPerTurn",
                schema: "game",
                table: "StructureInstances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Settlements",
                schema: "settlement",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    StructureInstanceId = table.Column<int>(type: "int", nullable: false),
                    FoundingPlayerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    Population = table.Column<int>(type: "int", nullable: false),
                    FoundedOnRoundNumber = table.Column<int>(type: "int", nullable: false),
                    FoundedOnTurnId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlements_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Settlements_Player_FoundingPlayerId",
                        column: x => x.FoundingPlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Settlements_StructureInstances_StructureInstanceId",
                        column: x => x.StructureInstanceId,
                        principalSchema: "game",
                        principalTable: "StructureInstances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMap_OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap",
                column: "OwnedBySettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_FoundingPlayerId",
                schema: "settlement",
                table: "Settlements",
                column: "FoundingPlayerId");

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_Game_Name",
                schema: "settlement",
                table: "Settlements",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Settlement_StructureInstance",
                schema: "settlement",
                table: "Settlements",
                column: "StructureInstanceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Settlements_OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap",
                column: "OwnedBySettlementId",
                principalSchema: "settlement",
                principalTable: "Settlements",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Settlements_OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropTable(
                name: "Settlements",
                schema: "settlement");

            migrationBuilder.DropIndex(
                name: "IX_CampaignMap_OwnedBySettlementId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "BuildPoints",
                schema: "structure",
                table: "Structures");

            migrationBuilder.DropColumn(
                name: "BuildPointsPerTurn",
                schema: "game",
                table: "StructureInstances");
        }
    }
}
