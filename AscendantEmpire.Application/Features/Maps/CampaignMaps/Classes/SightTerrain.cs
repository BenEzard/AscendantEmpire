using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// One tile of the <b>world</b> map, carrying everything the sight rules and a seen tile's report need to know about it.
/// </summary>
/// <remarks>
/// <para>
/// Always read from the world map, never from a Player's own copy of it. A Player who has never seen a tile has <c>Unknown</c> terrain on their copy, and
/// judging sight or entry against that would let them see through a Jungle, or be shown water they could then walk onto.
/// </para>
/// <para>
/// The movement facts are here for the blinded-Observer reveal, which asks the movement rules whether a neighbour could have been entered — see
/// <see cref="BlockedSightReveal"/> and <see cref="ToMovementTargetTile"/>.
/// </para>
/// </remarks>
internal sealed record SightTerrain
{
    /// <summary>
    /// The world-map id of the tile, which Forces, Structures and the history feeds are all stored against.
    /// </summary>
    public required int CampaignMapId { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required BiomeType BiomeType { get; init; }

    public required CampaignMapGeographyType GeographyType { get; init; }

    /// <summary>
    /// Whether the Biome blocks sight — Jungle, Forest, Woodland, Mangrove or Void. A blocking tile is seen, and stops the view beyond it.
    /// </summary>
    public required bool DoesBlockSight { get; init; }

    /// <summary>
    /// How high the Geography stands, where Flat is zero. Higher ground than the tile before it stops the view beyond it.
    /// </summary>
    public required int RelativeElevation { get; init; }

    public required string GeographyName { get; init; }

    public required decimal GeographyMovementCost { get; init; }

    public required string BiomeName { get; init; }

    public required decimal BiomeMovementCost { get; init; }

    public required bool BiomeIsWater { get; init; }

    /// <summary>Whether the Geography itself is water, from <see cref="Domain.Entities.Maps.CampaignMaps.CampaignGeography.IsWater"/>.</summary>
    public required bool GeographyIsWater { get; init; }

    public required bool BiomeIsTraversable { get; init; }

    /// <summary>Whether the ground itself can be entered, from <see cref="Domain.Entities.Maps.CampaignMaps.CampaignGeography.IsTraversable"/>.</summary>
    public required bool GeographyIsTraversable { get; init; }

    /// <summary>
    /// The same tile in the shape the movement rules judge entry against.
    /// </summary>
    public MovementTargetTile ToMovementTargetTile()
    {
        return new MovementTargetTile
        {
            XCoord = X,
            YCoord = Y,
            GeographyType = GeographyType,
            GeographyName = GeographyName,
            GeographyMovementCost = GeographyMovementCost,
            BiomeName = BiomeName,
            BiomeMovementCost = BiomeMovementCost,
            BiomeIsWater = BiomeIsWater,
            GeographyIsWater = GeographyIsWater,
            BiomeIsTraversable = BiomeIsTraversable,
            GeographyIsTraversable = GeographyIsTraversable
        };
    }
}
