using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM37_AddCampaignMapDepletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FishAmount",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "FodderAmount",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "ForageAmount",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropColumn(
                name: "HuntAmount",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.CreateTable(
                name: "CampaignMapDepletion",
                schema: "game",
                columns: table => new
                {
                    CampaignMapId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    ForageTaken = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    HuntTaken = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FishTaken = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FodderTaken = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FedOnRound = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMapDepletion", x => x.CampaignMapId);
                    table.CheckConstraint("CK_CampaignMapDepletion_TakenNonNegative", "[ForageTaken] >= 0 AND [HuntTaken] >= 0 AND [FishTaken] >= 0 AND [FodderTaken] >= 0");
                    table.ForeignKey(
                        name: "FK_CampaignMapDepletion_CampaignMap_CampaignMapId",
                        column: x => x.CampaignMapId,
                        principalSchema: "game",
                        principalTable: "CampaignMap",
                        principalColumn: "CampaignMapId");
                    table.ForeignKey(
                        name: "FK_CampaignMapDepletion_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMapDepletion_GameId",
                schema: "game",
                table: "CampaignMapDepletion",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMapDepletion",
                schema: "game");

            migrationBuilder.AddColumn<int>(
                name: "FishAmount",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FodderAmount",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForageAmount",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HuntAmount",
                schema: "game",
                table: "CampaignMap",
                type: "int",
                nullable: true);
        }
    }
}
