using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Everything the movement rules need to know about the tile a Force is trying to enter.
/// </summary>
/// <remarks>
/// <para>
/// A flat snapshot built once by the handler, so no EF entity reaches the rule layer and a strategy never has to query anything. This mirrors
/// <c>ForceActionContext</c>, which serves the context-action pipeline the same way.
/// </para>
/// <para>
/// The derived members below are the movement rules themselves. They live here rather than in the handler because they are pure arithmetic over
/// this snapshot, which makes them testable without a database.
/// </para>
/// </remarks>
internal sealed record MovementTargetTile
{
    public required int XCoord { get; init; }

    public required int YCoord { get; init; }

    public required CampaignMapGeographyType GeographyType { get; init; }

    /// <summary>The geography display name, used to explain a refusal in terms the player recognises.</summary>
    public required string GeographyName { get; init; }

    public required decimal GeographyMovementCost { get; init; }

    /// <summary>The biome display name, used to explain a refusal in terms the player recognises.</summary>
    public required string BiomeName { get; init; }

    public required decimal BiomeMovementCost { get; init; }

    public required bool BiomeIsWater { get; init; }

    public required bool BiomeIsTraversable { get; init; }

    /// <summary>
    /// Whether this tile is water, for the purpose of deciding which movement domains may enter it.
    /// </summary>
    /// <remarks>
    /// Both the biome and the geography record this — Coastal and Ocean carry <see cref="CampaignBiome.IsWater"/>, and the geography has its own
    /// deep and shallow water types — and the map generator keeps the two consistent. Either being water is enough, so a hand-edited map that sets
    /// only one of them still refuses a land Force rather than quietly letting it walk out to sea.
    /// </remarks>
    public bool IsWater => BiomeIsWater || GeographyType is CampaignMapGeographyType.DeepWater or CampaignMapGeographyType.ShallowWater;

    /// <summary>
    /// Whether nothing can enter this tile at all, whatever its movement domain or how much movement it has left.
    /// </summary>
    /// <remarks>
    /// Two independent markers, matching how the seed data expresses the idea: Mountainous uses the geography's
    /// <see cref="CampaignGeography.IMPASSABLE_MOVEMENT_COST"/> sentinel, while the Void biome is simply not traversable.
    /// </remarks>
    public bool IsImpassable => !BiomeIsTraversable || GeographyMovementCost >= CampaignGeography.IMPASSABLE_MOVEMENT_COST;

    /// <summary>
    /// What it costs to cross this tile, in movement points.
    /// </summary>
    /// <remarks>
    /// The geography is the ground and the biome is a modifier on it, so the biome contributes only its excess over the neutral cost of 1. That way
    /// ordinary Grassland over Flat stays at 1, Desert's 1.1 adds its tenth, and Hilly's 2 is not doubled merely by having a biome painted on it.
    /// Meaningless when <see cref="IsImpassable"/> is set, so check that first.
    /// </remarks>
    public decimal MovementCost => GeographyMovementCost + (BiomeMovementCost - 1m);

    /// <summary>
    /// Names the tile for a refusal message, for example "Hilly Forest at (12, 7)".
    /// </summary>
    public string Describe()
    {
        return $"{GeographyName} {BiomeName} at ({XCoord}, {YCoord})";
    }
}
