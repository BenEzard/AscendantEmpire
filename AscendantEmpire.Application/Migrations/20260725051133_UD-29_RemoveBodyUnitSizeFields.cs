using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD29_RemoveBodyUnitSizeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.DropColumn(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.DropColumn(
                name: "SizeIncrement",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.DropColumn(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SizeIncrement",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaximumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumUnitSize",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
