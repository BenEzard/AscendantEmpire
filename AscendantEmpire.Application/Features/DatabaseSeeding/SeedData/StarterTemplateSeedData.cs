using AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The global <c>unitdesigns.UnitTemplate</c> rows applied by the database seed: the starter units every new game spawns for every player.
/// </summary>
/// <remarks>
/// <para>
/// These are the two codes <c>GameSetupService.StarterTemplateCodes</c> demands — matched case-insensitively, so the lower-case codes here satisfy its
/// <c>SETTLER</c> and <c>SCOUT</c>. Without them game creation aborts rather than starting a game with no units.
/// </para>
/// <para>
/// The bulk-upsert compiles each template as it saves, so a seeded template arrives with a live snapshot and needs no separate recompile pass.
/// </para>
/// <para>
/// <b>Map images must be bundled filenames.</b> The campaign map resolves <c>MapImagePath</c> only against the frontend's <c>src/assets/units/*</c> files, and a
/// unit whose image does not resolve is not drawn at all — no placeholder, no marker. Both values below are bundled files.
/// </para>
/// </remarks>
internal static class StarterTemplateSeedData
{
    /// <summary>
    /// Builds the starter template rows to seed.
    /// </summary>
    public static CatalogueTemplatesDocument Build()
    {
        return new CatalogueTemplatesDocument
        {
            Templates =
            [
                new CatalogueTemplateDto
                {
                    Code = "settler",
                    DisplayName = "Settler",
                    MapImagePath = "settlers_40px.png",
                    BaseCode = "human",

                    // slot-6 is the body's Equipment slot; the camping materials carry the settler organisation tag.
                    SlotComponentCodes = new Dictionary<string, string> { ["slot-6"] = "camping-materials" },
                },
                new CatalogueTemplateDto
                {
                    Code = "scout",
                    DisplayName = "Scout",
                    MapImagePath = "tracker_40px.png",
                    BaseCode = "human",

                    // training-1 is the body's Training slot; the recon training carries the scout organisation tag.
                    SlotComponentCodes = new Dictionary<string, string> { ["training-1"] = "basic-recon" },
                },
            ],
        };
    }
}
