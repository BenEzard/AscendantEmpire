using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS9_AddResourceCatalogueColumnsAndCodeConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceGroup",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceCategory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "Resource",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "resources",
                table: "Resource",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "resources",
                table: "Resource",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetaCategory_Code",
                schema: "resources",
                table: "ResourceMetaCategory",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroup_Code",
                schema: "resources",
                table: "ResourceGroup",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCategory_Code",
                schema: "resources",
                table: "ResourceCategory",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceMetaCategory_Code",
                schema: "resources",
                table: "ResourceMetaCategory");

            migrationBuilder.DropIndex(
                name: "IX_ResourceGroup_Code",
                schema: "resources",
                table: "ResourceGroup");

            migrationBuilder.DropIndex(
                name: "IX_ResourceCategory_Code",
                schema: "resources",
                table: "ResourceCategory");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "resources",
                table: "Resource");

            migrationBuilder.AlterColumn<string>(
                name: "IconPath",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceMetaCategory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceGroup",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "ResourceCategory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "resources",
                table: "Resource",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
