using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentAndTemplateSupersession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows to TagType.Buff (1): every tag predating this column was a
            // buff/bonus tag. New rows always set TagType explicitly, so this default is only a backfill.
            migrationBuilder.AddColumn<int>(
                name: "TagType",
                schema: "unitdesigns",
                table: "Tag",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ComponentSupersession",
                schema: "unitdesigns",
                columns: table => new
                {
                    PredecessorComponentId = table.Column<int>(type: "int", nullable: false),
                    SuccessorComponentId = table.Column<int>(type: "int", nullable: false),
                    IsRecommended = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentSupersession", x => new { x.PredecessorComponentId, x.SuccessorComponentId });
                    table.ForeignKey(
                        name: "FK_ComponentSupersession_UnitComponent_PredecessorComponentId",
                        column: x => x.PredecessorComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComponentSupersession_UnitComponent_SuccessorComponentId",
                        column: x => x.SuccessorComponentId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitComponent",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemplateSupersession",
                schema: "unitdesigns",
                columns: table => new
                {
                    PredecessorTemplateId = table.Column<int>(type: "int", nullable: false),
                    SuccessorTemplateId = table.Column<int>(type: "int", nullable: false),
                    IsRecommended = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateSupersession", x => new { x.PredecessorTemplateId, x.SuccessorTemplateId });
                    table.ForeignKey(
                        name: "FK_TemplateSupersession_UnitTemplate_PredecessorTemplateId",
                        column: x => x.PredecessorTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemplateSupersession_UnitTemplate_SuccessorTemplateId",
                        column: x => x.SuccessorTemplateId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitTemplate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentSupersession_Successor",
                schema: "unitdesigns",
                table: "ComponentSupersession",
                column: "SuccessorComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSupersession_Successor",
                schema: "unitdesigns",
                table: "TemplateSupersession",
                column: "SuccessorTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentSupersession",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "TemplateSupersession",
                schema: "unitdesigns");

            migrationBuilder.DropColumn(
                name: "TagType",
                schema: "unitdesigns",
                table: "Tag");
        }
    }
}
