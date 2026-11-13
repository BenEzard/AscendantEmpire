using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD64_AddCompiledSupplyRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FoodQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FoodSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FuelQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FuelSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RepairQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FoodQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "FoodSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "FuelQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "FuelSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "RepairQuantity",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "RepairSupplySetId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");
        }
    }
}
