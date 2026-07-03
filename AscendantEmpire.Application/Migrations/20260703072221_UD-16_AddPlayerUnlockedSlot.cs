using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD16_AddPlayerUnlockedSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerUnlockedSlot",
                schema: "unitdesigns",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitComponentSlotId = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnTurn = table.Column<int>(type: "int", nullable: false),
                    ObsoleteOnTurn = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedSlot", x => new { x.PlayerId, x.UnitComponentSlotId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedSlot_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedSlot_UnitComponentSlot_UnitComponentSlotId",
                        column: x => x.UnitComponentSlotId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponentSlot",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedSlot_UnitComponentSlot",
                schema: "unitdesigns",
                table: "PlayerUnlockedSlot",
                column: "UnitComponentSlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerUnlockedSlot",
                schema: "unitdesigns");
        }
    }
}
