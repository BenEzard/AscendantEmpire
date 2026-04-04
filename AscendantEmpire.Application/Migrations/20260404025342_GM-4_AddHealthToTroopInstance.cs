using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM4_AddHealthToTroopInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HealthyCount",
                schema: "units",
                table: "TroopInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InitialStrength",
                schema: "units",
                table: "TroopInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InjuredCount",
                schema: "units",
                table: "TroopInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthyCount",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropColumn(
                name: "InitialStrength",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropColumn(
                name: "InjuredCount",
                schema: "units",
                table: "TroopInstances");
        }
    }
}
