using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class CM57_AddCampaignMapHasClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasClaims",
                schema: "game",
                table: "CampaignMap",
                type: "bit",
                nullable: false,
                defaultValue: false)
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_HasClaims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasClaims",
                schema: "game",
                table: "CampaignMap")
                .Annotation("Relational:DefaultConstraintName", "DF_CampaignMap_HasClaims");
        }
    }
}
