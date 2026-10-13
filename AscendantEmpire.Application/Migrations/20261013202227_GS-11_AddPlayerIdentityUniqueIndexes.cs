using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <summary>
    /// Makes a player's faction identity unique within its game: unique indexes over RulerName, FactionName, PeopleName, Demonym, CultureName and the
    /// chosen RulerPortraitPath, with a case-insensitive collation pinned on the five names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The collation is what makes the name indexes mean what they say. Nothing else in the solution sets one, so until now the comparison rested on
    /// how the server that created the database happened to be configured - on a CS_AS server "The Vashti" and "the vashti" would have been two
    /// different names. Pinning it per column makes that a decision somebody can review rather than an ambient default.
    /// </para>
    /// <para>
    /// The five name indexes are unfiltered: the columns are non-nullable and the validator refuses a blank, so there is no "undecided" value to
    /// spare. The portrait's is filtered to exempt the empty default, which every player who has not chosen a face holds at once - the same shape as
    /// the existing UX_Player_Game_FactionColour, with an empty string in place of its NULL.
    /// </para>
    /// <para>
    /// Rows predating the people-and-culture columns can hold blanks in them, which this will refuse once more than one such row shares a game. Those
    /// have to be filled in before it applies.
    /// </para>
    /// </remarks>
    public partial class GS11_AddPlayerIdentityUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RulerName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PeopleName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "FactionName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Demonym",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "CultureName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Latin1_General_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_CultureName",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "CultureName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_Demonym",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "Demonym" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_FactionName",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "FactionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_PeopleName",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "PeopleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_RulerName",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "RulerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Player_Game_RulerPortraitPath",
                schema: "game",
                table: "Player",
                columns: new[] { "GameId", "RulerPortraitPath" },
                unique: true,
                filter: "[RulerPortraitPath] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Player_Game_CultureName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropIndex(
                name: "UX_Player_Game_Demonym",
                schema: "game",
                table: "Player");

            migrationBuilder.DropIndex(
                name: "UX_Player_Game_FactionName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropIndex(
                name: "UX_Player_Game_PeopleName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropIndex(
                name: "UX_Player_Game_RulerName",
                schema: "game",
                table: "Player");

            migrationBuilder.DropIndex(
                name: "UX_Player_Game_RulerPortraitPath",
                schema: "game",
                table: "Player");

            migrationBuilder.AlterColumn<string>(
                name: "RulerName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "PeopleName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "FactionName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Demonym",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "CultureName",
                schema: "game",
                table: "Player",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Latin1_General_CI_AS");
        }
    }
}
