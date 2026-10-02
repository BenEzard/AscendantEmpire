using AscendantEmpire.Domain.Enums.MapResources;

namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// A MapResourceType as the map editor adds or updates it, with everything it owns: its yields, habitats, Distribution scenarios and layers.
/// </summary>
/// <remarks>
/// <para>
/// Quantities are whole numbers here and scaled by 1,000 on the way into the database, as the Biome Manager's values are. Likelihoods and percentages are fractions
/// (0.25 is 25%).
/// </para>
/// <para>
/// Which of the values and children must be set depends on the type's kind, set by <see cref="IsRenewable"/>, <see cref="IsStratified"/> and <see cref="IsDistribution"/>.
/// <c>MapResourceTypeValidator</c> checks every combination; see <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// A type's position within its category is not here: the manager does not edit it. The server sets it when the type is created and leaves it alone afterwards, and
/// <see cref="GetMapResourceCatalogue.IMapResourceTypeDetail.SortOrder"/> reports it.
/// </para>
/// </remarks>
public interface IAddOrUpdateMapResourceTypeDto
{
    #region General Properties
    /// <summary>
    /// Unique, stable key, e.g. "Berry". What an update matches on.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// The name shown to Players and in the map editor.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// The code of the category the map editor lists the type under.
    /// </summary>
    /// <remarks>
    /// A code rather than an id, so a category and the types in it can be created in the same save, and seed data can name categories without knowing their ids.
    /// </remarks>
    string MapResourceCategoryCode { get; }

    /// <summary>
    /// The path of the image drawn on a tile holding an instance, or <see langword="null"/> for no icon.
    /// </summary>
    string? IconPath { get; }
    #endregion

    #region Kind
    /// <summary>
    /// Grows every Round, and may spread. Cannot be set with <see cref="IsStratified"/>.
    /// </summary>
    bool IsRenewable { get; }

    /// <summary>
    /// Held in layers, reached in order. Never renewable.
    /// </summary>
    bool IsStratified { get; }

    /// <summary>
    /// Holds several Assets in set amounts from one of its scenarios. Must also be renewable, and never spreads.
    /// </summary>
    bool IsDistribution { get; }
    #endregion

    #region Extraction
    /// <summary>
    /// How instances are extracted.
    /// </summary>
    ExtractionMethod ExtractionMethod { get; }

    /// <summary>
    /// The chance a Capture succeeds, as a fraction. Set exactly when <see cref="ExtractionMethod"/> is Capture.
    /// </summary>
    decimal? CaptureLikelihood { get; }

    /// <summary>
    /// Whether a Player must research the type before they can extract it.
    /// </summary>
    bool RequiresResearchToExtract { get; }
    #endregion

    #region Discovery
    /// <summary>
    /// Whether Players can discover instances before any mechanic switches discovery on for them.
    /// </summary>
    bool IsDiscoverableByDefault { get; }

    /// <summary>
    /// The chance, as a fraction, that a unit on the tile discovers an instance (for a stratified type, its first layer).
    /// </summary>
    decimal DiscoveryLikelihood { get; }

    /// <summary>
    /// The lowest fuzzy factor an instance is created with, as a fraction. Empty for a stratified type, whose layers have their own.
    /// </summary>
    decimal? EstimationFuzzyFactorMin { get; }

    /// <summary>
    /// The highest fuzzy factor an instance is created with, as a fraction. Empty for a stratified type.
    /// </summary>
    decimal? EstimationFuzzyFactorMax { get; }
    #endregion

    #region Exhaustion (renewable only)
    /// <summary>
    /// How many Rounds a dormant instance survives. Set exactly when the type is renewable.
    /// </summary>
    int? SurvivalRounds { get; }

    /// <summary>
    /// The chance, as a fraction, that a dormant instance comes back each Round. Set exactly when the type is renewable.
    /// </summary>
    decimal? ReseedLikelihood { get; }
    #endregion

    /// <summary>
    /// The Assets one unit extracted gives. Only for a type that is neither a Distribution nor stratified, which must have at least one.
    /// </summary>
    IReadOnlyCollection<IMapResourceTypeYieldDto> Yields { get; }

    /// <summary>
    /// Where the type can exist, and its values there. Every type needs at least one.
    /// </summary>
    IReadOnlyCollection<IMapResourceHabitatDto> Habitats { get; }

    /// <summary>
    /// The scenarios a Distribution is created from. Only for a Distribution, which must have at least one.
    /// </summary>
    IReadOnlyCollection<IMapResourceDistributionScenarioDto> DistributionScenarios { get; }

    /// <summary>
    /// The layers of a stratified type, numbered 1, 2, 3... from the top. Only for a stratified type, which must have at least one.
    /// </summary>
    IReadOnlyCollection<IMapResourceTypeLayerDto> Layers { get; }
}
