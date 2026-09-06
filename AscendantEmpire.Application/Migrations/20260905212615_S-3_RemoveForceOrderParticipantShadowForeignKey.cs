using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class S3_RemoveForceOrderParticipantShadowForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForceOrderParticipants_ForceOrders_ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ForceOrderParticipants_ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants");

            migrationBuilder.DropColumn(
                name: "ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForceOrderParticipants_ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants",
                column: "ForceOrderId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ForceOrderParticipants_ForceOrders_ForceOrderId1",
                schema: "units",
                table: "ForceOrderParticipants",
                column: "ForceOrderId1",
                principalSchema: "units",
                principalTable: "ForceOrders",
                principalColumn: "Id");
        }
    }
}
