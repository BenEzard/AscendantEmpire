using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS24_AddWrappingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HorizontalWrap",
                schema: "game",
                table: "GameIndex",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VerticalWrap",
                schema: "game",
                table: "GameIndex",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HorizontalWrap",
                schema: "mapgallery",
                table: "CampaignMapIndex",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VerticalWrap",
                schema: "mapgallery",
                table: "CampaignMapIndex",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId",
                schema: "game",
                table: "Players",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_GameIndex_GameId",
                schema: "game",
                table: "Players",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_GameIndex_GameId",
                schema: "game",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_GameId",
                schema: "game",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HorizontalWrap",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropColumn(
                name: "VerticalWrap",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropColumn(
                name: "HorizontalWrap",
                schema: "mapgallery",
                table: "CampaignMapIndex");

            migrationBuilder.DropColumn(
                name: "VerticalWrap",
                schema: "mapgallery",
                table: "CampaignMapIndex");
        }
    }
}
