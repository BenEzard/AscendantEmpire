using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

/// <summary>
/// One gallery placement, with the terrain of the tile it sits on, ready to be turned into a Game's instance.
/// </summary>
/// <remarks>
/// Carries the tile's coordinates rather than its id: the coordinates are what every roll keys on, and they address the Game's tile through the Game's own
/// offset. See the Randomness section of <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
internal sealed record PlacementToInstantiate(
    int PlacementId,
    int XCoord,
    int YCoord,
    byte Slot,
    int MapResourceTypeId,
    long? InitialQuantity,
    int? MapResourceDistributionScenarioId,
    BiomeType BiomeTypeId,
    CampaignMapGeographyType CampaignMapGeographyTypeId);

/// <summary>
/// What one <c>map.MapResourceHabitat</c> row says an instance on its tiles is created with.
/// </summary>
/// <remarks>
/// Separate from <see cref="HabitatScope"/>, which says only where a habitat applies: the scope is the key these values are found by.
/// </remarks>
internal sealed record HabitatValues(
    long? InitialQuantityMin,
    long? InitialQuantityMax,
    int? SpreadLimitMin,
    int? SpreadLimitMax);

/// <summary>
/// One Distribution scenario: the Biomes it can appear in, how often, and the amounts an instance created from it starts with.
/// </summary>
/// <param name="ScenarioNumber">
/// The scenario's number within its type, which the weighted pick orders by. Its <paramref name="ScenarioId"/> is an identity value and so cannot be:
/// ordering by it would make the pick depend on the order that one database happened to insert the catalogue in.
/// </param>
internal sealed record ScenarioValues(
    int ScenarioId,
    int ScenarioNumber,
    IReadOnlyDictionary<BiomeType, int> BiomeWeights,
    IReadOnlyCollection<AmountToCreate> Amounts);

/// <summary>
/// One layer template of a stratified MapResourceType, and the ranges each instance's copy of it is rolled from.
/// </summary>
internal sealed record LayerValues(
    int LayerNumber,
    int AssetId,
    decimal EmptyLikelihood,
    long QuantityMin,
    long QuantityMax,
    decimal EstimationFuzzyFactorMin,
    decimal EstimationFuzzyFactorMax);

/// <summary>
/// What one <c>map.MapResourceType</c> and its child rows say an instance of it is created with.
/// </summary>
/// <remarks>
/// <para>
/// Shared reference data rather than a work item, unlike <see cref="PlacementToInstantiate"/>: one of these serves every placement of the type, and a type may
/// end up with no instances at all if each of its placements is refused.
/// </para>
/// <para>
/// The habitats are keyed by scope because <c>UX_MapResourceHabitat_Type_Biome_Geography</c> makes the scope unique within a type, which is what lets
/// <c>BiomeImpliedResourceRules.FindHabitat</c> decide precedence over the keys alone and the winner's values then be looked up.
/// </para>
/// </remarks>
internal sealed record MapResourceTypeValues(
    int MapResourceTypeId,
    string DisplayName,
    bool IsStratified,
    bool IsDistribution,
    decimal? EstimationFuzzyFactorMin,
    decimal? EstimationFuzzyFactorMax,
    IReadOnlyDictionary<HabitatScope, HabitatValues> Habitats,
    IReadOnlyCollection<ScenarioValues> Scenarios,
    IReadOnlyCollection<LayerValues> Layers);

/// <summary>
/// One Asset amount a Distribution instance is created holding.
/// </summary>
internal sealed record AmountToCreate(int AssetId, long Amount);

/// <summary>
/// One layer a stratified instance is created holding, with its contents already rolled.
/// </summary>
/// <remarks>
/// A layer that rolled empty keeps its row, with no Asset and nothing in it, because a survey can reveal that a layer is empty.
/// </remarks>
internal sealed record LayerToCreate(int LayerNumber, int? AssetId, long Quantity, decimal EstimationFuzzyFactor);

/// <summary>
/// One <c>game.MapResourceInstance</c> to create, with every rolled value settled and its child rows alongside it.
/// </summary>
/// <remarks>
/// <c>CreatedOnRound</c> and <c>DormantSinceRound</c> are not carried: every instance game setup creates is on Round 0 and active, so there is nothing for a
/// caller to decide.
/// </remarks>
internal sealed record InstanceToCreate(
    int CampaignMapId,
    byte Slot,
    int MapResourceTypeId,
    long? Quantity,
    int? SpreadLimit,
    decimal? EstimationFuzzyFactor,
    int? MapResourceDistributionScenarioId,
    IReadOnlyCollection<AmountToCreate> Amounts,
    IReadOnlyCollection<LayerToCreate> Layers);

/// <summary>
/// Why a placement could not be turned into an instance.
/// </summary>
/// <remarks>
/// None of these fails the Game's setup. The first two follow from repainting a tile's Biome after placing on it, which is a map-authoring problem, and the
/// last two from a catalogue a type's own validator should have refused.
/// </remarks>
internal enum MapResourceInstantiationRefusal
{
    /// <summary>
    /// No habitat of the placement's MapResourceType covers the tile's Biome and Geography, so the type cannot exist there.
    /// </summary>
    NoHabitat,

    /// <summary>
    /// A Distribution whose tile's Biome carries no scenario weight, leaving the weighted pick nothing to choose from.
    /// </summary>
    NoScenarioForBiome,

    /// <summary>
    /// The map editor pinned a Distribution scenario that belongs to a different MapResourceType.
    /// </summary>
    ScenarioNotOfType,

    /// <summary>
    /// A stratified MapResourceType with no layer templates, so its instance would hold nothing at all.
    /// </summary>
    NoLayers,

    /// <summary>
    /// A single-quantity MapResourceType whose habitat names no initial quantity range, on a placement the map editor set no quantity on.
    /// </summary>
    /// <remarks>
    /// The instance would hold nothing, could never be extracted, and would occupy one of the tile's two slots for the life of the Game.
    /// <c>MapResourceTypeValidator</c> requires the range, so this is a catalogue written around the validator.
    /// </remarks>
    NoQuantityRange
}

/// <summary>
/// A placement left uncreated, and why.
/// </summary>
/// <param name="MapResourceTypeDisplayName">
/// What the type is called, so a log can name it. Carried rather than looked up again, because a refusal for an absent type has no type to look up.
/// </param>
internal sealed record RefusedPlacement(
    int PlacementId,
    int XCoord,
    int YCoord,
    int MapResourceTypeId,
    string MapResourceTypeDisplayName,
    MapResourceInstantiationRefusal Reason);

/// <summary>
/// Every instance a Game should be created with, and every placement that could not become one.
/// </summary>
internal sealed record MapResourceInstantiationPlan(
    IReadOnlyCollection<InstanceToCreate> ToCreate,
    IReadOnlyCollection<RefusedPlacement> Refusals)
{
    /// <summary>
    /// Nothing to create and nothing refused, for a map that carries no placements at all.
    /// </summary>
    public static MapResourceInstantiationPlan None { get; } = new([], []);
}
