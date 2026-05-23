using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class Fix_BattleParticipant_Player_FK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BattleParticipant_Player_PlayerId1",
                schema: "battle",
                table: "BattleParticipant");

            migrationBuilder.DropIndex(
                name: "IX_BattleParticipant_PlayerId1",
                schema: "battle",
                table: "BattleParticipant");

            migrationBuilder.DropColumn(
                name: "PlayerId1",
                schema: "battle",
                table: "BattleParticipant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayerId1",
                schema: "battle",
                table: "BattleParticipant",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BattleParticipant_PlayerId1",
                schema: "battle",
                table: "BattleParticipant",
                column: "PlayerId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BattleParticipant_Player_PlayerId1",
                schema: "battle",
                table: "BattleParticipant",
                column: "PlayerId1",
                principalSchema: "game",
                principalTable: "Player",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
