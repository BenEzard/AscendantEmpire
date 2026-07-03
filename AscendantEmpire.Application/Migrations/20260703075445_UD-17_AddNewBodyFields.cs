using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD17_AddNewBodyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "CampaignMovementPoints",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<long>(
                name: "BattlefieldMovementPoints",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AddColumn<long>(
                name: "Hitpoints",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Power",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "CampaignMovementPoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<long>(
                name: "BattlefieldMovementPoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AddColumn<long>(
                name: "Hitpoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Power",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hitpoints",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.DropColumn(
                name: "Power",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.DropColumn(
                name: "Hitpoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "Power",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.AlterColumn<decimal>(
                name: "CampaignMovementPoints",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "BattlefieldMovementPoints",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "CampaignMovementPoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "BattlefieldMovementPoints",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
