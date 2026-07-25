using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AscendantEmpire.Application.Migrations
{
    /// <inheritdoc />
    public partial class G20_RemoveTagTypeHasDataSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The TagType catalogue is no longer seeded via EF HasData — the database seed tool now owns it.
            //
            // We intentionally DO NOT delete the existing rows here (EF scaffolded a DeleteData for each, but
            // that was removed): on a populated database those rows are referenced by unitdesigns.Tag via
            // FK_Tag_TagType_TagType, so deleting them fails. The seed tool overwrites the rows in place
            // (UpsertTagTypesHandler updates by key and never deletes), which keeps the FK satisfied.
            //
            // This migration therefore only exists to sync the model snapshot after the HasData block was
            // removed from AscendantEmpireContext; it makes no schema or data change.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: Up changed no data, so there is nothing to restore.
        }
    }
}
