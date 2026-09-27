using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS10_AddPlayerResourceUnlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerUnlockedResource",
                schema: "resources",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnRound = table.Column<int>(type: "int", nullable: false),
                    ObsoleteOnRound = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedResource", x => new { x.PlayerId, x.ResourceId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedResource_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedResource_Resource_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "resources",
                        principalTable: "Resource",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnlockedResourceMetaCategory",
                schema: "resources",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    ResourceMetaCategoryId = table.Column<int>(type: "int", nullable: false),
                    UnlockedOnRound = table.Column<int>(type: "int", nullable: false),
                    ObsoleteOnRound = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnlockedResourceMetaCategory", x => new { x.PlayerId, x.ResourceMetaCategoryId });
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedResourceMetaCategory_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory_ResourceMetaCategoryId",
                        column: x => x.ResourceMetaCategoryId,
                        principalSchema: "resources",
                        principalTable: "ResourceMetaCategory",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedResource_Resource",
                schema: "resources",
                table: "PlayerUnlockedResource",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnlockedResourceMetaCategory_ResourceMetaCategory",
                schema: "resources",
                table: "PlayerUnlockedResourceMetaCategory",
                column: "ResourceMetaCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerUnlockedResource",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "PlayerUnlockedResourceMetaCategory",
                schema: "resources");
        }
    }
}
