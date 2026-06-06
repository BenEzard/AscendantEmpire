using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS4_AddStandaloneResourceCategorySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ResourceGroupId",
                schema: "resources",
                table: "Resource",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ResourceCategoryId",
                schema: "resources",
                table: "Resource",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resource_ResourceCategoryId",
                schema: "resources",
                table: "Resource",
                column: "ResourceCategoryId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Resource_GroupOrCategoryXor",
                schema: "resources",
                table: "Resource",
                sql: "([ResourceGroupId] IS NOT NULL AND [ResourceCategoryId] IS NULL) OR ([ResourceGroupId] IS NULL AND [ResourceCategoryId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Resource_ResourceCategory_ResourceCategoryId",
                schema: "resources",
                table: "Resource",
                column: "ResourceCategoryId",
                principalSchema: "resources",
                principalTable: "ResourceCategory",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resource_ResourceCategory_ResourceCategoryId",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropIndex(
                name: "IX_Resource_ResourceCategoryId",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Resource_GroupOrCategoryXor",
                schema: "resources",
                table: "Resource");

            migrationBuilder.DropColumn(
                name: "ResourceCategoryId",
                schema: "resources",
                table: "Resource");

            migrationBuilder.AlterColumn<int>(
                name: "ResourceGroupId",
                schema: "resources",
                table: "Resource",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
