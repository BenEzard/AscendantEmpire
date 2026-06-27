using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class DropBodyCrushAttack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrushAttack",
                schema: "unitdesigns",
                table: "UnitComponentBody");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InactiveAt",
                schema: "auth",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InactiveReasonType",
                schema: "auth",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InactiveAt",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InactiveReasonType",
                schema: "auth",
                table: "Users");

            migrationBuilder.AddColumn<long>(
                name: "CrushAttack",
                schema: "unitdesigns",
                table: "UnitComponentBody",
                type: "bigint",
                nullable: true);
        }
    }
}
