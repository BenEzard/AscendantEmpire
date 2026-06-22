using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSlotsToAnyComponentAndNestTemplateComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UnitComponentSlot_UnitComponentBody_UnitComponentBodyId",
                schema: "unitdesigns",
                table: "UnitComponentSlot");

            migrationBuilder.DropIndex(
                name: "UX_UnitTemplateComponent_Template_Slot",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.RenameColumn(
                name: "UnitComponentBodyId",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                newName: "UnitComponentId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitComponentSlot_Body",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                newName: "IX_UnitComponentSlot_Component");

            migrationBuilder.AddColumn<int>(
                name: "ParentTemplateComponentId",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTemplateComponent_Parent",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                column: "ParentTemplateComponentId");

            migrationBuilder.CreateIndex(
                name: "UX_UnitTemplateComponent_Nested",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                columns: new[] { "UnitTemplateId", "ParentTemplateComponentId", "UnitComponentSlotId" },
                unique: true,
                filter: "[ParentTemplateComponentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UnitTemplateComponent_Root",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                columns: new[] { "UnitTemplateId", "UnitComponentSlotId" },
                unique: true,
                filter: "[ParentTemplateComponentId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompiledUnitTemplateComponent_Parent",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompiledUnitTemplateComponent_CompiledUnitTemplateComponent_ParentId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent",
                column: "ParentId",
                principalSchema: "unitdesigns",
                principalTable: "CompiledUnitTemplateComponent",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UnitComponentSlot_UnitComponent_UnitComponentId",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                column: "UnitComponentId",
                principalSchema: "unitdesigns",
                principalTable: "UnitComponent",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UnitTemplateComponent_UnitTemplateComponent_ParentTemplateComponentId",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                column: "ParentTemplateComponentId",
                principalSchema: "unitdesigns",
                principalTable: "UnitTemplateComponent",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompiledUnitTemplateComponent_CompiledUnitTemplateComponent_ParentId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitComponentSlot_UnitComponent_UnitComponentId",
                schema: "unitdesigns",
                table: "UnitComponentSlot");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitTemplateComponent_UnitTemplateComponent_ParentTemplateComponentId",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.DropIndex(
                name: "IX_UnitTemplateComponent_Parent",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.DropIndex(
                name: "UX_UnitTemplateComponent_Nested",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.DropIndex(
                name: "UX_UnitTemplateComponent_Root",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.DropIndex(
                name: "IX_CompiledUnitTemplateComponent_Parent",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent");

            migrationBuilder.DropColumn(
                name: "ParentTemplateComponentId",
                schema: "unitdesigns",
                table: "UnitTemplateComponent");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplateComponent");

            migrationBuilder.RenameColumn(
                name: "UnitComponentId",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                newName: "UnitComponentBodyId");

            migrationBuilder.RenameIndex(
                name: "IX_UnitComponentSlot_Component",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                newName: "IX_UnitComponentSlot_Body");

            migrationBuilder.CreateIndex(
                name: "UX_UnitTemplateComponent_Template_Slot",
                schema: "unitdesigns",
                table: "UnitTemplateComponent",
                columns: new[] { "UnitTemplateId", "UnitComponentSlotId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitComponentSlot_UnitComponentBody_UnitComponentBodyId",
                schema: "unitdesigns",
                table: "UnitComponentSlot",
                column: "UnitComponentBodyId",
                principalSchema: "unitdesigns",
                principalTable: "UnitComponentBody",
                principalColumn: "UnitComponentId");
        }
    }
}
