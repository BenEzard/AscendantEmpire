using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Everything the per-Round territory rules need to know about one campaign map tile.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SightTerrain"/> of territory: a flat immutable snapshot of scalars the reader projects, with the rules that read them as computed members, 
/// so the whole pass is testable without a database. The caller loads the tiles; this says what each one is.
/// </para>
/// <para>
/// <b>There is deliberately nothing here about water.</b> Water is ordinary terrain for ownership - it is owned by the same three routes land is, 
/// and it is as sticky once owned - so the territory rules have no question to ask about it. See "Water" in <c>docs/design/campaign-map/tile-ownership.md</c>.
/// </para>
/// </remarks>
internal sealed record TerritoryTile
{
    /// <summary>The world-map tile id, which is what every delta is keyed by.</summary>
    public required int CampaignMapId { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public int? OwnedByPlayerId { get; init; }

    public int? OwnedBySettlementId { get; init; }

    public int? OccupiedByPlayerId { get; init; }

    /// <summary>The former owner of an Abandoned tile; <see langword="null"/> on ground nobody has ever held.</summary>
    public int? LastOwnedByPlayerId { get; init; }

    /// <summary>Every Player with a claim row on this tile. Empty for nearly every tile on the map.</summary>
    public required IReadOnlyCollection<int> ClaimantPlayerIds { get; init; }

    /// <summary>
    /// What <c>CampaignMap.HasClaims</c> currently says.
    /// </summary>
    /// <remarks>
    /// Carried so the pass can <i>repair</i> the flag where it disagrees with <see cref="ClaimantPlayerIds"/>. The junction rows are the truth; the flag is the cache
    /// the pass keeps right.
    /// </remarks>
    public required bool HasClaims { get; init; }

    public required BiomeType BiomeTypeId { get; init; }

    public required bool BiomeIsTraversable { get; init; }

    /// <summary>Whether the ground itself can be entered, from <c>CampaignGeography.IsTraversable</c>. False only for Mountainous today.</summary>
    public required bool GeographyIsTraversable { get; init; }

    /// <summary>This tile's own South and East river columns. Its North and West edges belong to its neighbours.</summary>
    public required TileRiverEdges Rivers { get; init; }

    /// <summary>Where the tile sits, for the neighbour arithmetic.</summary>
    public Coord Location => new(X, Y);

    /// <summary>The tile's key in the pass's bitmap - a tuple rather than a <see cref="Coord"/>, which is a class and would allocate on every lookup.</summary>
    public (int X, int Y) Key => (X, Y);

    /// <summary>
    /// Whether nothing can enter this tile at all - a mountain, or the Void.
    /// </summary>
    /// <remarks>
    /// Does two jobs in the territory rules, and they are deliberately the same fact movement already knows (<see cref="CampaignTileTerrain"/>): 
    /// an impassable tile <b>seals</b> an enclosure ring, and it <b>carries no claim supply hop</b>, so an owned mountain is a dead end rather than a bridge.
    /// </remarks>
    public bool IsImpassable => CampaignTileTerrain.IsImpassable(BiomeIsTraversable, GeographyIsTraversable);

    /// <summary>
    /// Whether this tile is the Void, which can never be owned or claimed by anybody.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="IsImpassable"/> on purpose, because the two mean different things: the Void is unownable, while a mountain is perfectly ownable and
    /// merely unwalkable. Today the Void is the only non-traversable biome, so one implies the other - separating them is what makes a future impassable-but-ownable
    /// terrain work with no change here.
    /// </remarks>
    public bool IsVoid => BiomeTypeId == BiomeType.Void;

    /// <summary>
    /// Whether anybody has any interest in this tile at all.
    /// </summary>
    /// <remarks>
    /// The test for wilderness, and so for what enclosure may absorb. A former owner counts: an Abandoned tile is held by nobody but is not wilderness either, and
    /// absorbing it would let a neighbour inherit a razed empire.
    /// </remarks>
    public bool IsHeld => OwnedByPlayerId is not null
                       || OccupiedByPlayerId is not null
                       || LastOwnedByPlayerId is not null
                       || ClaimantPlayerIds.Count > 0;
}
