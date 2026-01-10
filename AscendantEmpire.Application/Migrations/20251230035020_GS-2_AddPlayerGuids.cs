using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GS2_AddPlayerGuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_Index_GameId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_Index_GameId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Index",
                schema: "game",
                table: "Index");

            migrationBuilder.RenameTable(
                name: "Index",
                schema: "game",
                newName: "GameIndex",
                newSchema: "game");

            migrationBuilder.RenameColumn(
                name: "GameIndexId",
                schema: "game",
                table: "GameIndex",
                newName: "Id");

            migrationBuilder.AddColumn<Guid>(
                name: "Guid",
                schema: "game",
                table: "Player",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.NewGuid());

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameIndex",
                schema: "game",
                table: "GameIndex",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_GameIndex_GameId",
                schema: "game",
                table: "CampaignMap",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_GameIndex_GameId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "GameId",
                principalSchema: "game",
                principalTable: "GameIndex",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignMap_GameIndex_GameId",
                schema: "game",
                table: "CampaignMap");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerCampaignMap_GameIndex_GameId",
                schema: "game",
                table: "PlayerCampaignMap");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GameIndex",
                schema: "game",
                table: "GameIndex");

            migrationBuilder.DropColumn(
                name: "Guid",
                schema: "game",
                table: "Player");

            migrationBuilder.RenameTable(
                name: "GameIndex",
                schema: "game",
                newName: "Index",
                newSchema: "game");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "game",
                table: "Index",
                newName: "GameIndexId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Index",
                schema: "game",
                table: "Index",
                column: "GameIndexId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignMap_Index_GameId",
                schema: "game",
                table: "CampaignMap",
                column: "GameId",
                principalSchema: "game",
                principalTable: "Index",
                principalColumn: "GameIndexId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerCampaignMap_Index_GameId",
                schema: "game",
                table: "PlayerCampaignMap",
                column: "GameId",
                principalSchema: "game",
                principalTable: "Index",
                principalColumn: "GameIndexId");
        }
    }
}
