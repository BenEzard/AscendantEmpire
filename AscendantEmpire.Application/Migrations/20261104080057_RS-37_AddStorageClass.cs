using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS37_AddStorageClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageClassId",
                schema: "assets",
                table: "Asset",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StorageClass",
                schema: "assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageClass", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asset_StorageClassId",
                schema: "assets",
                table: "Asset",
                column: "StorageClassId");

            migrationBuilder.CreateIndex(
                name: "UX_StorageClass_Code",
                schema: "assets",
                table: "StorageClass",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StorageClass_DisplayName",
                schema: "assets",
                table: "StorageClass",
                column: "DisplayName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Asset_StorageClass_StorageClassId",
                schema: "assets",
                table: "Asset",
                column: "StorageClassId",
                principalSchema: "assets",
                principalTable: "StorageClass",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asset_StorageClass_StorageClassId",
                schema: "assets",
                table: "Asset");

            migrationBuilder.DropTable(
                name: "StorageClass",
                schema: "assets");

            migrationBuilder.DropIndex(
                name: "IX_Asset_StorageClassId",
                schema: "assets",
                table: "Asset");

            migrationBuilder.DropColumn(
                name: "StorageClassId",
                schema: "assets",
                table: "Asset");
        }
    }
}
