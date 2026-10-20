using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The territory pass's scratch copy of a Game's ownership: what each rule has decided so far, and the diff that falls out at the end.
/// </summary>
/// <remarks>
/// <para>
/// The sub-rules write here rather than emitting changes, and <see cref="ToChanges"/> compares the result with the <see cref="TerritoryMap"/> it was built from. Two
/// things follow from that, and both matter:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>"Ownership trumps claim" has exactly one implementation</b> - <see cref="AssertOwnership"/> clears the tile's claimants. Nothing else removes a claim except
///     <see cref="LapseClaim"/>, so the invariant cannot be half-applied.
///   </description></item>
///   <item><description>
///     <b>A claim proposed and then withdrawn in the same pass produces no delta at all.</b> Enclosure grants the middle of a wide pocket a claim and the supply range
///     takes it straight back; without the diff that would insert and delete the same row every Round for ever.
///   </description></item>
/// </list>
/// <para>
/// Per-Player indexes of owned and claimed tiles are maintained on every write, so the supply range seeds its walk from a Player's own territory rather than rescanning
/// a 40,000-tile map once per Player.
/// </para>
/// </remarks>
internal sealed class TerritoryWorkingSet
{
    /// <summary>No claimants, shared by every tile that has none - which is nearly all of them.</summary>
    private static readonly int[] _NO_CLAIMANTS = [];

    private readonly Dictionary<(int X, int Y), int?> _ownerByCoord = [];
    private readonly Dictionary<(int X, int Y), int?> _settlementByCoord = [];
    private readonly Dictionary<(int X, int Y), HashSet<int>> _claimantsByCoord = [];

    /// <summary>Tiles whose ownership grant also clears an Abandoned stamp, so the writer knows to null the remembered owner.</summary>
    private readonly HashSet<(int X, int Y)> _abandonmentCleared = [];

    private readonly Dictionary<int, HashSet<(int X, int Y)>> _ownedTilesByPlayer = [];
    private readonly Dictionary<int, HashSet<(int X, int Y)>> _claimedTilesByPlayer = [];

    private TerritoryWorkingSet(TerritoryMap map, IReadOnlyCollection<int> playerIds)
    {
        Map = map;
        PlayerIds = playerIds;

        foreach (TerritoryTile tile in map.TilesByCoord.Values)
        {
            _ownerByCoord[tile.Key] = tile.OwnedByPlayerId;
            _settlementByCoord[tile.Key] = tile.OwnedBySettlementId;

            if (tile.OwnedByPlayerId is int owner)
            {
                Index(_ownedTilesByPlayer, owner, tile.Key);
            }

            if (tile.ClaimantPlayerIds.Count == 0)
            {
                continue;
            }

            _claimantsByCoord[tile.Key] = [.. tile.ClaimantPlayerIds];

            foreach (int claimant in tile.ClaimantPlayerIds)
            {
                Index(_claimedTilesByPlayer, claimant, tile.Key);
            }
        }
    }

    internal TerritoryMap Map { get; }

    internal CampaignMapGeometry Geometry => Map.Geometry;

    /// <summary>
    /// Every Player the pass considers, in turn queue order.
    /// </summary>
    /// <remarks>
    /// Turn order rather than id order, so that where two Players could want one tile the earlier Player in the queue gets it - and so no write in the pass depends on
    /// the order a dictionary happened to enumerate in.
    /// </remarks>
    internal IReadOnlyCollection<int> PlayerIds { get; }

    internal static TerritoryWorkingSet From(TerritoryMap map, IReadOnlyCollection<int> playerIdsInTurnOrder)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(playerIdsInTurnOrder);

        return new TerritoryWorkingSet(map, playerIdsInTurnOrder);
    }

    /// <summary>Who owns the tile as the pass has it now.</summary>
    internal int? GetOwner((int X, int Y) coord)
    {
        return _ownerByCoord.GetValueOrDefault(coord);
    }

    /// <summary>Every Player claiming the tile as the pass has it now.</summary>
    internal IReadOnlyCollection<int> GetClaimants((int X, int Y) coord)
    {
        return _claimantsByCoord.TryGetValue(coord, out HashSet<int>? claimants) ? claimants : _NO_CLAIMANTS;
    }

    /// <summary>Whether the tile is held by anybody at all as the pass has it now - the test for wilderness enclosure may absorb.</summary>
    internal bool IsHeld((int X, int Y) coord)
    {
        if (GetOwner(coord) is not null || GetClaimants(coord).Count > 0)
        {
            return true;
        }

        TerritoryTile? tile = Map.Get(coord);

        // Occupation and a remembered former owner are not written by this pass, so they are read straight off the loaded tile.
        return tile is not null && (tile.OccupiedByPlayerId is not null || tile.LastOwnedByPlayerId is not null);
    }

    /// <summary>Every tile this Player owns now, which is where the supply range's walk starts.</summary>
    internal IReadOnlyCollection<(int X, int Y)> GetOwnedTiles(int playerId)
    {
        return _ownedTilesByPlayer.TryGetValue(playerId, out HashSet<(int X, int Y)>? owned) ? owned : [];
    }

    /// <summary>Every tile this Player claims now.</summary>
    internal IReadOnlyCollection<(int X, int Y)> GetClaimedTiles(int playerId)
    {
        return _claimedTilesByPlayer.TryGetValue(playerId, out HashSet<(int X, int Y)>? claimed) ? claimed : [];
    }

    /// <summary>
    /// Makes the tile owned by <paramref name="playerId"/>, extinguishing every claim on it.
    /// </summary>
    /// <remarks>
    /// <b>The one place "ownership trumps claim" is spent.</b> The doc is explicit that the two never coexist on a tile, so a tile becoming owned clears every rival
    /// claim - and the owner's own, which has been superseded by something better.
    /// </remarks>
    /// <param name="settlementId">The Settlement the tile is owned through, or <see langword="null"/> for owned land that carries none.</param>
    internal void AssertOwnership((int X, int Y) coord, int playerId, int? settlementId)
    {
        if (_ownerByCoord.TryGetValue(coord, out int? previousOwner) && previousOwner is int outgoing)
        {
            _ownedTilesByPlayer[outgoing].Remove(coord);
        }

        _ownerByCoord[coord] = playerId;
        _settlementByCoord[coord] = settlementId;
        Index(_ownedTilesByPlayer, playerId, coord);

        // A grant extinguishes what the ground remembered as much as what anyone asserted over it.
        if (Map.Get(coord)?.LastOwnedByPlayerId is not null)
        {
            _abandonmentCleared.Add(coord);
        }

        if (_claimantsByCoord.TryGetValue(coord, out HashSet<int>? claimants) == false)
        {
            return;
        }

        foreach (int claimant in claimants)
        {
            _claimedTilesByPlayer[claimant].Remove(coord);
        }

        _claimantsByCoord.Remove(coord);
    }

    /// <summary>Adds a claim for <paramref name="playerId"/>, which is non-exclusive: several Players may claim one tile.</summary>
    internal void AssertClaim((int X, int Y) coord, int playerId)
    {
        if (_claimantsByCoord.TryGetValue(coord, out HashSet<int>? claimants) == false)
        {
            claimants = [];
            _claimantsByCoord[coord] = claimants;
        }

        if (claimants.Add(playerId))
        {
            Index(_claimedTilesByPlayer, playerId, coord);
        }
    }

    /// <summary>Removes one Player's claim, leaving any rival claim on the same tile alone.</summary>
    internal void LapseClaim((int X, int Y) coord, int playerId)
    {
        if (_claimantsByCoord.TryGetValue(coord, out HashSet<int>? claimants) == false || claimants.Remove(playerId) == false)
        {
            return;
        }

        _claimedTilesByPlayer[playerId].Remove(coord);

        if (claimants.Count == 0)
        {
            _claimantsByCoord.Remove(coord);
        }
    }

    /// <summary>
    /// The difference between what the pass decided and the map it was given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HasClaims</c> is emitted for every tile whose final claimant count disagrees with the flag the tile came in with - which includes a tile the pass did not
    /// otherwise touch. The pass holds the complete claim set for the Game, so a flag saying <c>true</c> over no rows at all is simply wrong, and repairing it costs
    /// nothing. That is the one thing <c>TileOwnershipDerivation</c> currently has to tolerate.
    /// </para>
    /// <para>
    /// Ownership is only ever <i>granted</i>: there is no provenance column saying why a tile is owned, so the pass could not tell a Settlement's ground from an
    /// enclosure's and must never take ownership away. That is also what makes the pass monotone, and so idempotent.
    /// </para>
    /// </remarks>
    internal TerritoryChanges ToChanges()
    {
        Dictionary<int, TileOwnershipWrite> ownershipWrites = [];
        HashSet<(int CampaignMapId, int PlayerId)> claimsToAdd = [];
        HashSet<(int CampaignMapId, int PlayerId)> claimsToRemove = [];
        Dictionary<int, bool> hasClaimsWrites = [];

        foreach (TerritoryTile tile in Map.TilesByCoord.Values)
        {
            int? owner = GetOwner(tile.Key);
            int? settlement = _settlementByCoord.GetValueOrDefault(tile.Key);

            if (owner != tile.OwnedByPlayerId || settlement != tile.OwnedBySettlementId)
            {
                ownershipWrites[tile.CampaignMapId] = new TileOwnershipWrite(
                    tile.CampaignMapId,
                    owner,
                    settlement,
                    ClearsAbandonment: _abandonmentCleared.Contains(tile.Key));
            }

            IReadOnlyCollection<int> finalClaimants = GetClaimants(tile.Key);

            foreach (int added in finalClaimants.Except(tile.ClaimantPlayerIds))
            {
                claimsToAdd.Add((tile.CampaignMapId, added));
            }

            foreach (int removed in tile.ClaimantPlayerIds.Except(finalClaimants))
            {
                claimsToRemove.Add((tile.CampaignMapId, removed));
            }

            bool shouldHaveClaims = finalClaimants.Count > 0;

            if (shouldHaveClaims != tile.HasClaims)
            {
                hasClaimsWrites[tile.CampaignMapId] = shouldHaveClaims;
            }
        }

        return new TerritoryChanges
        {
            OwnershipWrites = ownershipWrites,
            ClaimsToAdd = claimsToAdd,
            ClaimsToRemove = claimsToRemove,
            HasClaimsWrites = hasClaimsWrites
        };
    }

    private static void Index(Dictionary<int, HashSet<(int X, int Y)>> index, int playerId, (int X, int Y) coord)
    {
        if (index.TryGetValue(playerId, out HashSet<(int X, int Y)>? tiles) == false)
        {
            tiles = [];
            index[playerId] = tiles;
        }

        tiles.Add(coord);
    }
}
