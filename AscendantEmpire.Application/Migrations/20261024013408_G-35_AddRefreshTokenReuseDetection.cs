using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class G35_AddRefreshTokenReuseDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FamilyId is added in three steps rather than the single non-null column EF scaffolds, because that one defaults every existing row to
            // Guid.Empty — putting every session that predates this migration into one shared family. The first reuse detection would then revoke all
            // of them at once, logging out every user who happened to be signed in across the deploy. Backfilling a distinct family per row keeps live
            // sessions working and keeps a family meaning "one login".
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                schema: "auth",
                table: "RefreshSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE [auth].[RefreshSessions] SET [FamilyId] = NEWID() WHERE [FamilyId] IS NULL;");

            // No default constraint on the way to NOT NULL: every insert comes from IssueAsync, which always supplies a family.
            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyId",
                schema: "auth",
                table: "RefreshSessions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRotatedAt",
                schema: "auth",
                table: "RefreshSessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousTokenHash",
                schema: "auth",
                table: "RefreshSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevokedReason",
                schema: "auth",
                table: "RefreshSessions",
                type: "int",
                nullable: true);

            // No defaultValue, unlike the scaffolded version: SQL Server refuses a default constraint on a rowversion column ("Defaults cannot be
            // created on columns with a timestamp data type") and populates every existing row itself when the column is added.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "auth",
                table: "RefreshSessions",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSession_ExpiresAt",
                schema: "auth",
                table: "RefreshSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSession_Family",
                schema: "auth",
                table: "RefreshSessions",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "UX_RefreshSession_PreviousTokenHash",
                schema: "auth",
                table: "RefreshSessions",
                column: "PreviousTokenHash",
                unique: true,
                filter: "[PreviousTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshSession_ExpiresAt",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshSession_Family",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropIndex(
                name: "UX_RefreshSession_PreviousTokenHash",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "LastRotatedAt",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "PreviousTokenHash",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                schema: "auth",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "auth",
                table: "RefreshSessions");
        }
    }
}
