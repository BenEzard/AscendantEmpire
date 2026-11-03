using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class RS35_AddSettlementParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentSettlementId",
                schema: "settlement",
                table: "Settlements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_ParentSettlementId",
                schema: "settlement",
                table: "Settlements",
                column: "ParentSettlementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Settlements_ParentSettlementId",
                schema: "settlement",
                table: "Settlements",
                column: "ParentSettlementId",
                principalSchema: "settlement",
                principalTable: "Settlements",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Settlements_ParentSettlementId",
                schema: "settlement",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_ParentSettlementId",
                schema: "settlement",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "ParentSettlementId",
                schema: "settlement",
                table: "Settlements");
        }
    }
}
