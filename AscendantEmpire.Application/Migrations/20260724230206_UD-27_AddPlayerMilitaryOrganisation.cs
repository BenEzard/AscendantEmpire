using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD27_AddPlayerMilitaryOrganisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "military");

            migrationBuilder.CreateTable(
                name: "PlayerOfficerRank",
                schema: "military",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MovementDomain = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxSoldiers = table.Column<int>(type: "int", nullable: true),
                    MaxAttachedSpecialists = table.Column<int>(type: "int", nullable: false),
                    InUse = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerOfficerRank", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerOfficerRank_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerOrganisationStructure",
                schema: "military",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerOrganisationStructure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerOrganisationStructure_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerOrganisationStructure_Tag_TagId",
                        column: x => x.TagId,
                        principalSchema: "unitdesigns",
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerOrganisationStructureTier",
                schema: "military",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StructureId = table.Column<int>(type: "int", nullable: false),
                    TierIndex = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Multiplier = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerOrganisationStructureTier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerOrganisationStructureTier_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "military",
                        principalTable: "PlayerOrganisationStructure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PlayerOfficerRank_Player_Domain_Level",
                schema: "military",
                table: "PlayerOfficerRank",
                columns: new[] { "PlayerId", "MovementDomain", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerOrganisationStructure_TagId",
                schema: "military",
                table: "PlayerOrganisationStructure",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerOrganisationStructure_Player_Tag",
                schema: "military",
                table: "PlayerOrganisationStructure",
                columns: new[] { "PlayerId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlayerOrganisationStructureTier_Structure_TierIndex",
                schema: "military",
                table: "PlayerOrganisationStructureTier",
                columns: new[] { "StructureId", "TierIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerOfficerRank",
                schema: "military");

            migrationBuilder.DropTable(
                name: "PlayerOrganisationStructureTier",
                schema: "military");

            migrationBuilder.DropTable(
                name: "PlayerOrganisationStructure",
                schema: "military");
        }
    }
}
