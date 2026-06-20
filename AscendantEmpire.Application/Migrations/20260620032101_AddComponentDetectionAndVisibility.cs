using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentDetectionAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Detection",
                schema: "unitdesigns",
                table: "UnitComponent",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Visibility",
                schema: "unitdesigns",
                table: "UnitComponent",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Detection",
                schema: "unitdesigns",
                table: "UnitComponent");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "unitdesigns",
                table: "UnitComponent");
        }
    }
}
