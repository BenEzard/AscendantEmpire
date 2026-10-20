using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The columns the territory reader projects off one campaign map tile, before its claimants are joined on.
/// </summary>
/// <remarks>
/// A separate shape from <see cref="TerritoryTile"/> for one reason: the claimants come from the junction rather than the tile, so they cannot be part of a projection
/// that runs as SQL. This is what the database hands back; <see cref="TerritoryTile"/> is what the rules read.
/// </remarks>
internal sealed record TerritoryTileColumns
{
    public required int CampaignMapId { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public int? OwnedByPlayerId { get; init; }

    public int? OwnedBySettlementId { get; init; }

    public int? OccupiedByPlayerId { get; init; }

    public int? LastOwnedByPlayerId { get; init; }

    public required bool HasClaims { get; init; }

    public required BiomeType BiomeTypeId { get; init; }

    public required bool BiomeIsTraversable { get; init; }

    public required decimal GeographyMovementCost { get; init; }

    public required RiverWidth SouthRiver { get; init; }

    public required RiverWidth EastRiver { get; init; }
}
