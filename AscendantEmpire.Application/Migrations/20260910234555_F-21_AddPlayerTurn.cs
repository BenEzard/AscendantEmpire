using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class F21_AddPlayerTurn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerTurn",
                schema: "turns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnQueueId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAllocatedSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTurn", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTurn_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerTurn_TurnQueue_TurnQueueId",
                        column: x => x.TurnQueueId,
                        principalSchema: "turns",
                        principalTable: "TurnQueue",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTurn_PlayerId",
                schema: "turns",
                table: "PlayerTurn",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTurn_Queue_Player",
                schema: "turns",
                table: "PlayerTurn",
                columns: new[] { "TurnQueueId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTurn_Queue_Round",
                schema: "turns",
                table: "PlayerTurn",
                columns: new[] { "TurnQueueId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "UX_PlayerTurn_OpenTurnPerQueue",
                schema: "turns",
                table: "PlayerTurn",
                column: "TurnQueueId",
                unique: true,
                filter: "[EndedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerTurn",
                schema: "turns");
        }
    }
}
