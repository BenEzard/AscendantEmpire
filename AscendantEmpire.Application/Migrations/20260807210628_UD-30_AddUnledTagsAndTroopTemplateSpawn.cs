using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class UD30_AddUnledTagsAndTroopTemplateSpawn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TroopInstances previously referenced the legacy UnitDesign; the column below is renamed and
            // re-pointed at UnitTemplate, so existing rows would carry design ids that are meaningless (or
            // wrong) as template ids. All troop data to date is dev-only, so it is deleted rather than
            // migrated — children first, because every FK in the chain is NoAction.
            migrationBuilder.Sql("DELETE FROM [troops].[TroopOrderMovementSteps];");
            migrationBuilder.Sql("DELETE FROM [troops].[TroopOrderParticipants];");
            migrationBuilder.Sql("DELETE FROM [troops].[TroopOrders];");
            migrationBuilder.Sql("DELETE FROM [units].[TroopInstances];");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_UnitDesigns_UnitDesignId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.RenameColumn(
                name: "UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                newName: "UnitTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                newName: "IX_TroopInstances_UnitTemplateId");

            migrationBuilder.AddColumn<int>(
                name: "LeadershipModel",
                schema: "unitdesigns",
                table: "Tag",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnledBaseSize",
                schema: "unitdesigns",
                table: "Tag",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnledDefaultSize",
                schema: "unitdesigns",
                table: "Tag",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnledMaxSize",
                schema: "unitdesigns",
                table: "Tag",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompiledUnitTemplate_OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                column: "OrganisationTagId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompiledUnitTemplate_Tag_OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate",
                column: "OrganisationTagId",
                principalSchema: "unitdesigns",
                principalTable: "Tag",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_UnitTemplate_UnitTemplateId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitTemplateId",
                principalSchema: "unitdesigns",
                principalTable: "UnitTemplate",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompiledUnitTemplate_Tag_OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_UnitTemplate_UnitTemplateId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropIndex(
                name: "IX_CompiledUnitTemplate_OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.DropColumn(
                name: "LeadershipModel",
                schema: "unitdesigns",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "UnledBaseSize",
                schema: "unitdesigns",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "UnledDefaultSize",
                schema: "unitdesigns",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "UnledMaxSize",
                schema: "unitdesigns",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "OrganisationTagId",
                schema: "unitdesigns",
                table: "CompiledUnitTemplate");

            migrationBuilder.RenameColumn(
                name: "UnitTemplateId",
                schema: "units",
                table: "TroopInstances",
                newName: "UnitDesignId");

            migrationBuilder.RenameIndex(
                name: "IX_TroopInstances_UnitTemplateId",
                schema: "units",
                table: "TroopInstances",
                newName: "IX_TroopInstances_UnitDesignId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_UnitDesigns_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitDesignId",
                principalSchema: "unitdesigns",
                principalTable: "UnitDesigns",
                principalColumn: "Id");
        }
    }
}
