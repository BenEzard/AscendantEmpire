using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD12_RenameFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinStrength",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "SizeIncrement");

            migrationBuilder.RenameColumn(
                name: "MaxStrength",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "MinimumUnitSize");

            migrationBuilder.RenameColumn(
                name: "IncrementStrength",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "MaximumUnitSize");

            migrationBuilder.RenameColumn(
                name: "MinStrength",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                newName: "MinimumUnitSize");

            migrationBuilder.RenameColumn(
                name: "MaxStrength",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                newName: "MaximumUnitSize");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "unitdesigns",
                table: "Tag",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SizeIncrement",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "MinStrength");

            migrationBuilder.RenameColumn(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "MaxStrength");

            migrationBuilder.RenameColumn(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                newName: "IncrementStrength");

            migrationBuilder.RenameColumn(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                newName: "MinStrength");

            migrationBuilder.RenameColumn(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                newName: "MaxStrength");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "unitdesigns",
                table: "Tag",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
