using AscendantEmpire.BackendContracts.Technologies;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>technology.ExperienceType</c> catalogue applied by the database seed: the kinds of experience an action can earn.
/// </summary>
/// <remarks>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a row rather than duplicating it, and never removes one added by hand. Codes are spelled out
/// explicitly rather than derived, unlike an Asset's: each producing system resolves its own concept against this exact string, so a code must not shift when
/// someone rewords a display name.
/// </para>
/// <para>
/// The families below are the dimensions experience actually arrives along. <b>Biome</b> types are earned by being somewhere - a Force crossing jungle, a
/// settlement founded on arid ground - and so there is one per real <c>BiomeType</c> member, excluding the two that are not terrain. <b>Extraction</b> is
/// earned proportionally to what a settlement or camp takes from a tile, <b>Refining</b> by turning one Asset into another, <b>Manufacture</b> by producing
/// units and equipment, and <b>Husbandry</b> by keeping animals. <b>Exploration</b> stands alone: it is earned by uncovering terrain and belongs to no family.
/// </para>
/// <para>
/// <b>Nothing writes any of these yet.</b> Movement, exploration, extraction, refining, manufacture and husbandry all have to call the experience allocator,
/// and most of those systems are not built - so this catalogue is deliberately authored ahead of its producers, and the experience bucket stays empty in play
/// until they land.
/// </para>
/// <para>
/// SortOrder is left unset throughout and derived from list position, spaced by 10, so the order things appear in this file is the order they appear in game
/// and there is no second set of numbers to keep in step with it. Icon paths are left unset until artwork exists; the UI substitutes a placeholder.
/// </para>
/// </remarks>
internal static class ExperienceTypeSeedData
{
    /// <summary>
    /// Builds the whole experience type catalogue.
    /// </summary>
    public static IReadOnlyCollection<UpsertExperienceTypeDto> Build()
    {
        return
        [
            // --- Exploration -----------------------------------------------------------
            new UpsertExperienceTypeDto
            {
                Code = "Exploration",
                DisplayName = "Exploration",
                Description = "Earned by uncovering terrain no unit of yours has seen before."
            },

            // --- Biomes ----------------------------------------------------------------
            // One per real BiomeType member. Unknown and Void are excluded: they are the absence of terrain rather than a kind of it.
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Coastal",
                DisplayName = "Coastal",
                Description = "Earned by operating on, and settling, the shoreline."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Ocean",
                DisplayName = "Ocean",
                Description = "Earned by operating in open water."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Desert",
                DisplayName = "Desert",
                Description = "Earned by operating in, and settling, true desert."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Arid",
                DisplayName = "Arid",
                Description = "Earned by operating in, and settling, dry scrubland."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Ice",
                DisplayName = "Ice",
                Description = "Earned by operating on permanent ice."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Tundra",
                DisplayName = "Tundra",
                Description = "Earned by operating in, and settling, frozen open ground."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Grassland",
                DisplayName = "Grassland",
                Description = "Earned by operating in, and settling, open grassland."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Woodland",
                DisplayName = "Woodland",
                Description = "Earned by operating in, and settling, light woodland."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Forest",
                DisplayName = "Forest",
                Description = "Earned by operating in, and settling, dense forest."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Biome.Jungle",
                DisplayName = "Jungle",
                Description = "Earned by operating in, and settling, jungle."
            },

            // --- Extraction ------------------------------------------------------------
            // Earned in proportion to how much was taken from the tile, so a rich seam teaches more than a poor one.
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Foraging",
                DisplayName = "Foraging",
                Description = "Earned by gathering wild plant food, in proportion to the amount taken."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Hunting",
                DisplayName = "Hunting",
                Description = "Earned by taking wild game, in proportion to the amount taken."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Fishing",
                DisplayName = "Fishing",
                Description = "Earned by taking fish from water, in proportion to the amount taken."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Woodcutting",
                DisplayName = "Woodcutting",
                Description = "Earned by felling timber, in proportion to the amount taken."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Quarrying",
                DisplayName = "Quarrying",
                Description = "Earned by cutting stone from the ground, in proportion to the amount taken."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Extraction.Mining",
                DisplayName = "Mining",
                Description = "Earned by extracting ore and minerals, in proportion to the amount taken."
            },

            // --- Refining --------------------------------------------------------------
            // Earned by a production process turning one Asset into another, rather than by taking anything from the ground.
            new UpsertExperienceTypeDto
            {
                Code = "Refining.StoneCutting",
                DisplayName = "Stone Cutting",
                Description = "Earned by working raw rock into dressed stone and blocks."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Refining.Woodworking",
                DisplayName = "Woodworking",
                Description = "Earned by working logs into planks and shaped timber."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Refining.Smelting",
                DisplayName = "Smelting",
                Description = "Earned by reducing ore to workable metal."
            },

            // --- Manufacture -----------------------------------------------------------
            new UpsertExperienceTypeDto
            {
                Code = "Manufacture.Swords",
                DisplayName = "Sword Making",
                Description = "Earned by producing bladed weapons."
            },
            new UpsertExperienceTypeDto
            {
                Code = "Manufacture.Cavalry",
                DisplayName = "Cavalry Training",
                Description = "Earned by raising and training mounted units."
            },

            // --- Husbandry -------------------------------------------------------------
            new UpsertExperienceTypeDto
            {
                Code = "Husbandry.Sheep",
                DisplayName = "Shepherding",
                Description = "Earned by keeping sheep, including as they reproduce."
            }
        ];
    }
}
