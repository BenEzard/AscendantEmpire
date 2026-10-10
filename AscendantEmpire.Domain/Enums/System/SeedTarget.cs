namespace AscendantEmpire.Domain.Enums.System;

/// <summary>
/// One independently selectable part of the database seed.
/// </summary>
/// <remarks>
/// <para>
/// Each member has exactly one <c>ISeedStep</c> implementation behind it, and the seed selection dialog lists one row per member. Adding a member without
/// adding its step fails <c>SeedStepCatalogueTests</c>, so the two can't drift apart.
/// </para>
/// <para>
/// Serialised as its string name (the API registers <c>JsonStringEnumConverter</c>), so the frontend's string union carries these names verbatim and the
/// numbers below are never seen outside the database. They are still assigned explicitly, because an expanded selection is persisted into a Processing
/// row's <c>MetadataJson</c> and a reordering would silently re-target a queued job.
/// </para>
/// </remarks>
public enum SeedTarget
{
    /// <summary>
    /// The <c>map.CampaignGeographies</c> catalogue — the elevation/movement half of a tile's terrain.
    /// </summary>
    Geographies = 1,

    /// <summary>
    /// The <c>map.CampaignBiomes</c> catalogue — the cover/asset half of a tile's terrain.
    /// </summary>
    Biomes = 2,

    /// <summary>
    /// The <c>map.MapResourceCategory</c> rows the map editor groups MapResource types under.
    /// </summary>
    MapResourceCategories = 3,

    /// <summary>
    /// The <c>unitdesigns.TagType</c> metadata catalogue, one row per <see cref="Domain.Enums.UnitDesigns.TagType"/> member.
    /// </summary>
    TagTypes = 4,

    /// <summary>
    /// The <c>unitdesigns.Tag</c> catalogue, including the military organisation tags the starting requirements turn on.
    /// </summary>
    Tags = 5,

    /// <summary>
    /// The <c>unitdesigns.UnitComponent</c> catalogue — the human body, its equipment and training, and the primitive weapons.
    /// </summary>
    UnitComponents = 6,

    /// <summary>
    /// The global <c>unitdesigns.UnitTemplate</c> rows a new game spawns for every player — the settler and the scout.
    /// </summary>
    StarterTemplates = 7,

    /// <summary>
    /// The <c>assets</c> schema catalogue — the meta-categories, categories, groups and Assets a Player's economy is built from.
    /// </summary>
    Assets = 8,

    /// <summary>
    /// The <c>technology.ExperienceType</c> catalogue — the kinds of experience an action can earn, which technologies then draw on.
    /// </summary>
    ExperienceTypes = 9,

    /// <summary>
    /// The <c>technology.Technology</c> catalogue, with its prerequisites, acquisition costs, grants and experience mappings.
    /// </summary>
    Technologies = 10,

    /// <summary>
    /// The <c>map.MapResourceType</c> rows and their children — Timber, and the mixes of wood a Forest, Jungle or Woodland tile can start with.
    /// </summary>
    MapResourceTypes = 11
}
