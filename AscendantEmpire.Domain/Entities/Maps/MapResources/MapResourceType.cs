using AscendantEmpire.Domain.Enums.MapResources;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// A kind of MapResource, e.g. Berry, Deer, Horse, Coal Seam or Forest Wood: everything about how its instances behave that is the same on every tile and in every game.
/// </summary>
/// <remarks>
/// <para>
/// Always "MapResource", never "resource", for the thing on the tile: a Resource is what extracting it gives. See <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Its kind is set by three flags, whose combinations are checked by <c>MapResourceTypeValidator</c>:
/// <list type="bullet">
///   <item><description><b>None set</b>: a fixed amount that never grows, giving its <see cref="Yields"/>.</description></item>
///   <item><description><see cref="IsRenewable"/>: grows every Round and may spread, giving its <see cref="Yields"/>.</description></item>
///   <item><description><see cref="IsRenewable"/> and <see cref="IsDistribution"/>: grows, never spreads, and holds several Resources in set amounts from one of its
///   <see cref="DistributionScenarios"/>.</description></item>
///   <item><description><see cref="IsStratified"/>: never grows, and holds its Resources in <see cref="Layers"/> reached in order.</description></item>
/// </list>
/// </para>
/// <para>
/// Where it can exist, and its values there, are in <see cref="Habitats"/>. A game's instances are <c>game.MapResourceInstance</c> rows.
/// </para>
/// </remarks>
public class MapResourceType
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    /// <summary>
    /// The same length as <c>Resource.MAX_ICON_PATH_LENGTH</c>, the other image path in the catalogue.
    /// </summary>
    public const int ICON_PATH_MAX_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key, e.g. "Berry". What seed data matches on.
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    /// <summary>
    /// The name shown to Players and in the map editor, e.g. "Berry".
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// The group the map editor lists this type under.
    /// </summary>
    public int MapResourceCategoryId { get; set; }

    public MapResourceCategory Category { get; set; } = null!;

    /// <summary>
    /// Where the type comes within its category in the map editor's list, lowest first.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// The path of the image drawn on a tile holding an instance, or <see langword="null"/> for a type with no icon.
    /// </summary>
    /// <remarks>
    /// Some types need none: a Distribution of wood in a forest is already said by the biome.
    /// </remarks>
    [MaxLength(ICON_PATH_MAX_LENGTH)]
    public string? IconPath { get; set; }

    #region Kind
    /// <summary>
    /// Whether instances grow every Round (and, unless a Distribution, may spread), and go dormant rather than vanish when exhausted.
    /// </summary>
    /// <remarks>
    /// Cannot be set together with <see cref="IsStratified"/>.
    /// </remarks>
    public bool IsRenewable { get; set; }

    /// <summary>
    /// Whether instances hold their Resources in <see cref="Layers"/>, reached in order. Never renewable.
    /// </summary>
    public bool IsStratified { get; set; }

    /// <summary>
    /// Whether instances hold several Resources in set amounts, taken from one of the <see cref="DistributionScenarios"/>. Must also be renewable, and never spreads.
    /// </summary>
    public bool IsDistribution { get; set; }
    #endregion

    #region Extraction
    /// <summary>
    /// How instances are extracted.
    /// </summary>
    public ExtractionMethod ExtractionMethod { get; set; }

    /// <summary>
    /// The chance that a <see cref="ExtractionMethod.Capture"/> succeeds, as a fraction (0.25 is 25%), or <see langword="null"/> for any other method.
    /// </summary>
    public decimal? CaptureLikelihood { get; set; }

    /// <summary>
    /// Whether a Player must research the type before they can extract it (e.g. Berry), rather than as soon as they discover an instance.
    /// </summary>
    public bool RequiresResearchToExtract { get; set; }
    #endregion

    #region Discovery
    /// <summary>
    /// Whether Players can discover instances before any mechanic switches discovery on for them.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> for something like uranium: placed at map generation, but undiscoverable until a late tech. See <c>PlayerMapResourceSwitch</c>.
    /// </remarks>
    public bool IsDiscoverableByDefault { get; set; }

    /// <summary>
    /// The chance, as a fraction, that a unit entering or resting on the tile discovers an instance. 1 for something found at once (special woods); 0.1 for copper.
    /// </summary>
    /// <remarks>
    /// For a stratified type this is the chance of finding the first layer; deeper layers have their own on <see cref="MapResourceTypeLayer"/>.
    /// </remarks>
    public decimal DiscoveryLikelihood { get; set; }

    /// <summary>
    /// The lowest fuzzy factor an instance can be created with, as a fraction (0.2 means the quantity shown can be up to 20% off).
    /// </summary>
    /// <remarks>
    /// Each instance rolls its own factor between this and <see cref="EstimationFuzzyFactorMax"/>. <see langword="null"/> for a stratified type, whose layers each have
    /// their own range.
    /// </remarks>
    public decimal? EstimationFuzzyFactorMin { get; set; }

    /// <summary>
    /// The highest fuzzy factor an instance can be created with. See <see cref="EstimationFuzzyFactorMin"/>.
    /// </summary>
    public decimal? EstimationFuzzyFactorMax { get; set; }
    #endregion

    #region Exhaustion (renewable only)
    /// <summary>
    /// How many Rounds an exhausted (dormant) instance survives before it is deleted, or <see langword="null"/> for a type that is not renewable.
    /// </summary>
    public int? SurvivalRounds { get; set; }

    /// <summary>
    /// The chance, as a fraction, that a dormant instance comes back in each Round it survives, or <see langword="null"/> for a type that is not renewable.
    /// </summary>
    public decimal? ReseedLikelihood { get; set; }
    #endregion

    /// <summary>
    /// The Resources one unit extracted gives. Empty for a Distribution or a stratified type, which hold their Resources themselves.
    /// </summary>
    public ICollection<MapResourceTypeYield> Yields { get; set; } = [];

    /// <summary>
    /// The Biomes and Geographies the type can exist on, with its values there. No habitat means the type can never exist on, or spread onto, that tile.
    /// </summary>
    public ICollection<MapResourceHabitat> Habitats { get; set; } = [];

    /// <summary>
    /// The sets of starting amounts a Distribution instance is created from. Empty for any other type.
    /// </summary>
    public ICollection<MapResourceDistributionScenario> DistributionScenarios { get; set; } = [];

    /// <summary>
    /// The layers a stratified instance is created with, from the top down. Empty for any other type.
    /// </summary>
    public ICollection<MapResourceTypeLayer> Layers { get; set; } = [];
}
