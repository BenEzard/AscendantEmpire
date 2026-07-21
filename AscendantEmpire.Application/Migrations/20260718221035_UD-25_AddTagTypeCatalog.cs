using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD25_AddTagTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TagType",
                schema: "unitdesigns",
                columns: table => new
                {
                    TagTypeId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsInherited = table.Column<bool>(type: "bit", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagType", x => x.TagTypeId);
                });

            migrationBuilder.InsertData(
                schema: "unitdesigns",
                table: "TagType",
                columns: new[] { "TagTypeId", "Colour", "Description", "DisplayName", "Icon", "IsInherited" },
                values: new object[,]
                {
                    { 0, null, "A structural classification of a component, such as its family or role.", "Classification", null, false },
                    { 1, null, "A temporary or permanent enhancement or buff to combat.", "Combat", null, false },
                    { 2, null, "A temporary or permanent enhancement or buff to movement.", "Movement", null, false },
                    { 3, null, "A unique capability or function of a unit component.", "Ability", null, false },
                    { 4, null, "The organisational group this fits into when organising the military into hierarchy.", "Military Organisational Group", null, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tag_TagType",
                schema: "unitdesigns",
                table: "Tag",
                column: "TagType");

            migrationBuilder.AddForeignKey(
                name: "FK_Tag_TagType_TagType",
                schema: "unitdesigns",
                table: "Tag",
                column: "TagType",
                principalSchema: "unitdesigns",
                principalTable: "TagType",
                principalColumn: "TagTypeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tag_TagType_TagType",
                schema: "unitdesigns",
                table: "Tag");

            migrationBuilder.DropTable(
                name: "TagType",
                schema: "unitdesigns");

            migrationBuilder.DropIndex(
                name: "IX_Tag_TagType",
                schema: "unitdesigns",
                table: "Tag");
        }
    }
}
