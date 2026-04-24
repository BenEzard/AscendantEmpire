using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM4_AddMovementPointsAndModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MovementPointsRemaining",
                schema: "units",
                table: "TroopInstances",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModifiersJson",
                schema: "unitdesigns",
                table: "CompiledDesignStats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MovementPoints",
                schema: "unitdesigns",
                table: "CompiledDesignStats",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MovementPointsRemaining",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropColumn(
                name: "ModifiersJson",
                schema: "unitdesigns",
                table: "CompiledDesignStats");

            migrationBuilder.DropColumn(
                name: "MovementPoints",
                schema: "unitdesigns",
                table: "CompiledDesignStats");
        }
    }
}
