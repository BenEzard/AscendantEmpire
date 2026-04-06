using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class GM4_RefactorUnitDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_CompiledDesignStats_UnitDesignId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns");

            migrationBuilder.CreateTable(
                name: "UnitDesigns",
                schema: "unitdesigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: true),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    DesignName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsTemplate = table.Column<bool>(type: "bit", nullable: false),
                    MapImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitDesigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitDesigns_GameIndex_GameId",
                        column: x => x.GameId,
                        principalSchema: "game",
                        principalTable: "GameIndex",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UnitDesigns_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompiledDesignStats",
                schema: "unitdesigns",
                columns: table => new
                {
                    UnitDesignId = table.Column<int>(type: "int", nullable: false),
                    SightRange = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompiledDesignStats", x => x.UnitDesignId);
                    table.ForeignKey(
                        name: "FK_CompiledDesignStats_UnitDesigns_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitDesigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerUnitDesignStates",
                schema: "unitdesigns",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UnitDesignId = table.Column<int>(type: "int", nullable: false),
                    IsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    HasBeenPrototyped = table.Column<bool>(type: "bit", nullable: false),
                    IsObsolete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUnitDesignStates", x => new { x.PlayerId, x.UnitDesignId });
                    table.ForeignKey(
                        name: "FK_PlayerUnitDesignStates_Player_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Player",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerUnitDesignStates_UnitDesigns_UnitDesignId",
                        column: x => x.UnitDesignId,
                        principalSchema: "unitdesigns",
                        principalTable: "UnitDesigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUnitDesignStates_UnitDesignId",
                schema: "unitdesigns",
                table: "PlayerUnitDesignStates",
                column: "UnitDesignId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDesigns_GameId",
                schema: "unitdesigns",
                table: "UnitDesigns",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitDesigns_PlayerId",
                schema: "unitdesigns",
                table: "UnitDesigns",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_UnitDesigns_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitDesignId",
                principalSchema: "unitdesigns",
                principalTable: "UnitDesigns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompiledDesignStats_UnitDesigns_UnitDesignId",
                schema: "unitdesigns",
                table: "CompiledDesignStats");

            migrationBuilder.DropForeignKey(
                name: "FK_TroopInstances_UnitDesigns_UnitDesignId",
                schema: "units",
                table: "TroopInstances");

            migrationBuilder.DropTable(
                name: "PlayerUnitDesignStates",
                schema: "unitdesigns");

            migrationBuilder.DropTable(
                name: "UnitDesigns",
                schema: "unitdesigns");

            migrationBuilder.AlterColumn<int>(
                name: "UnitDesignId",
                schema: "unitdesigns",
                table: "CompiledDesignStats",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "MapImagePath",
                schema: "unitdesigns",
                table: "CompiledDesignStats",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_TroopInstances_CompiledDesignStats_UnitDesignId",
                schema: "units",
                table: "TroopInstances",
                column: "UnitDesignId",
                principalSchema: "unitdesigns",
                principalTable: "CompiledDesignStats",
                principalColumn: "UnitDesignId");
        }
    }
}
