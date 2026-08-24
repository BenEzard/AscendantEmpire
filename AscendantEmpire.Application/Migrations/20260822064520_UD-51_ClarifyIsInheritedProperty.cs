using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD51_ClarifyIsInheritedProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsInherited",
                schema: "unitdesigns",
                table: "TagType",
                newName: "IsInheritedToBase");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsInheritedToBase",
                schema: "unitdesigns",
                table: "TagType",
                newName: "IsInherited");
        }
    }
}
