using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// Where a MapResourceType can exist - a Biome and optionally one Geography - and its values there.
/// </summary>
/// <remarks>
/// Quantities are whole numbers; likelihoods and percentages are fractions. Which values must be set depends on the type's kind.
/// </remarks>
public interface IMapResourceHabitatDto
{
    BiomeType BiomeType { get; }

    /// <summary>
    /// The Geography, or <see langword="null"/> for every Geography in the Biome. Each (Biome, Geography) appears at most once per type.
    /// </summary>
    CampaignMapGeographyType? GeographyType { get; }

    /// <summary>
    /// The lowest quantity an instance is rolled with at game setup. Set exactly when the type is neither a Distribution nor stratified.
    /// </summary>
    int? InitialQuantityMin { get; }

    /// <summary>
    /// The highest quantity an instance is rolled with at game setup. Set exactly when <see cref="InitialQuantityMin"/> is.
    /// </summary>
    int? InitialQuantityMax { get; }

    /// <summary>
    /// The ceiling a renewable instance grows to. Set exactly when the type is renewable.
    /// </summary>
    int? MaximumQuantity { get; }

    /// <summary>
    /// The share of its quantity a renewable instance grows each Round, as a fraction. Set exactly when the type is renewable.
    /// </summary>
    decimal? GrowthPercentage { get; }

    /// <summary>
    /// The chance each Round an instance at its ceiling spreads, as a fraction. Only for a renewable type that is not a Distribution, and set together with the
    /// SpreadLimit range or not at all.
    /// </summary>
    decimal? SpreadLikelihood { get; }

    /// <summary>
    /// The lowest SpreadLimit an instance is rolled with at game setup.
    /// </summary>
    int? SpreadLimitMin { get; }

    /// <summary>
    /// The highest SpreadLimit an instance is rolled with at game setup.
    /// </summary>
    int? SpreadLimitMax { get; }
}
