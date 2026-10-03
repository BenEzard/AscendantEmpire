using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One kind of experience a Player can earn by doing things, e.g. "Biome.Jungle" or "Extraction.Mining".
/// </summary>
/// <remarks>
/// <para>
/// Experience is a <b>typed currency with its own catalogue</b>, rather than a pointer at whatever produced it. The kinds in play span unrelated dimensions:
/// Jungle and Arid are <c>BiomeType</c> enum members, mining and woodcutting are extraction activities, sword development keys off a produced Asset, cavalry
/// off a unit kind, and exploration owns no row anywhere. A generic (SourceType, SourceId) pointer would hold enum values in some rows and table ids in others,
/// which is worse than the trade <c>ModifierSourceType</c> makes - there, at least, every value is a row id.
/// </para>
/// <para>
/// Each producing system maps its own concept to an <see cref="Id"/> once - movement resolves <c>BiomeType.Jungle</c> to the "Biome.Jungle" row - so the
/// Technology side never learns what a biome is, and "Exploration" gets a home despite owning no row of its own.
/// </para>
/// <para>
/// <b>Nothing writes experience yet.</b> Movement, exploration, extraction, refining, manufacture and husbandry all have to call the allocator, and most of
/// those systems are not built, so this catalogue is authored ahead of its producers.
/// </para>
/// </remarks>
public sealed class ExperienceType
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_DESCRIPTION_LENGTH = 1000;
    public const int MAX_ICON_PATH_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique stable key, hierarchical by the family of activity that earns it.
    /// </summary>
    /// <remarks>
    /// What every upsert matches on, and what each producing system resolves its own concept against. Examples: "Biome.Jungle", "Biome.Arid", "Exploration",
    /// "Extraction.Mining", "Extraction.Woodcutting", "Extraction.Foraging", "Extraction.Hunting", "Refining.StoneCutting", "Manufacture.Swords",
    /// "Manufacture.Cavalry", "Husbandry.Sheep".
    /// </remarks>
    [MaxLength(MAX_CODE_LENGTH), Required]
    public required string Code { get; set; } = null!;

    [MaxLength(MAX_DISPLAY_NAME_LENGTH), Required]
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Flavour text describing what earns this kind of experience.
    /// </summary>
    [MaxLength(MAX_DESCRIPTION_LENGTH)]
    public string? Description { get; set; }

    [MaxLength(MAX_ICON_PATH_LENGTH), Required]
    public required string IconPath { get; set; } = null!;

    /// <summary>
    /// Display order in the Player's experience summary.
    /// </summary>
    /// <remarks>Spaced by 10 by the seed, so a type can be slotted between two others without renumbering the rest.</remarks>
    public required int SortOrder { get; set; }

    /// <summary>
    /// Which technologies this kind of experience feeds.
    /// </summary>
    public ICollection<TechnologyExperienceType> Technologies { get; set; } = [];
}
