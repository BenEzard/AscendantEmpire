using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class G136_AddTagTypeColourUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_TagType_Colour",
                schema: "unitdesigns",
                table: "TagType",
                column: "Colour",
                unique: true,
                filter: "[Colour] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_TagType_Colour",
                schema: "unitdesigns",
                table: "TagType");
        }
    }
}
