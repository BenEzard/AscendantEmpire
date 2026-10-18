using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="ObserverSightPresentation"/>: that what the map read resolved actually reaches the client.
/// </summary>
/// <remarks>
/// <para>
/// On the no-database tier. The mapper is a projection between two record shapes, so nothing here needs a context.
/// </para>
/// <para>
/// <b>Why this fixture exists.</b> The mapper lists every field by hand, so a field added to <see cref="ObservedCampaignTile"/> and not added here is served to the client as
/// <see langword="null"/> with nothing failing anywhere - no build error, no failing test, and a feature that is simply absent in the UI. That has now happened twice: once
/// to <c>ObserverRecordType</c>, which the class still carries a comment about, and once to <see cref="ObservedCampaignTile.Ownership"/>, which left the campaign map with no
/// borders on it however much ownership the database held. Both endpoints that return a Player's view of the map share this mapper, so the hole appears in the Force step too.
/// </para>
/// </remarks>
[TestFixture]
internal class ObserverSightPresentationTests
{
    private const int _OWNER = 7;
    private const int _OCCUPIER = 8;

    /// <summary>One Observer watching one Tile, which is all any of these need.</summary>
    private static ObserverSight SightOf(ObservedCampaignTile tile)
    {
        return new ObserverSight
        {
            Id = 1,
            RecordType = RecordType.Unit,
            Location = new Coord(12, 9),
            SeenTiles = [tile]
        };
    }

    /// <summary>A watched Tile carrying whatever ownership the test is about.</summary>
    private static ObservedCampaignTile TileWith(TileOwnership? ownership)
    {
        return new ObservedCampaignTile
        {
            XCoord = 12,
            YCoord = 9,
            BiomeTypeId = BiomeType.Grassland,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            IsExplored = true,
            Rivers = TileRivers.None,
            Ownership = ownership,
            Structures = [],
            Forces = []
        };
    }

    private static IObservedCampaignTile MapOne(ObservedCampaignTile tile)
    {
        return ObserverSightPresentation.ToPresentation([SightOf(tile)]).Single().SeenTiles!.Single();
    }

    [Test, Description("The regression: a resolved ownership record reaches the client with every layer intact, rather than being dropped by the hand-written mapping.")]
    public void ShouldCarryEveryOwnershipLayerThroughTheMapping()
    {
        var ownership = new TileOwnership
        {
            Status = TileOwnershipStatus.Occupied,
            OwnedByPlayerId = _OWNER,
            OccupiedByPlayerId = _OCCUPIER,
            LastOwnedByPlayerId = null,
            ClaimedByPlayerIds = [_OWNER, _OCCUPIER],
            IsRebelling = true
        };

        ITileOwnership? mapped = MapOne(TileWith(ownership)).Ownership;

        Assert.That(mapped, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(mapped!.Status, Is.EqualTo(TileOwnershipStatus.Occupied));
            Assert.That(mapped.OwnedByPlayerId, Is.EqualTo(_OWNER));
            Assert.That(mapped.OccupiedByPlayerId, Is.EqualTo(_OCCUPIER));
            Assert.That(mapped.LastOwnedByPlayerId, Is.Null);
            // Computed off the two columns rather than carried, so it has to survive as the occupier and not fall back to the owner.
            Assert.That(mapped.EffectiveHolderPlayerId, Is.EqualTo(_OCCUPIER));
            // The whole list, in order: the client draws one colour per claimant along the border's dashes and names them in that order.
            Assert.That(mapped.ClaimedByPlayerIds, Is.EqualTo(new[] { _OWNER, _OCCUPIER }));
            Assert.That(mapped.IsRebelling, Is.True);
        });
    }

    [Test, Description("A Tile no Observer can see keeps its null ownership, since standing in wilderness for it would draw a border off ground nobody is watching.")]
    public void ShouldKeepNullOwnershipRatherThanSubstitutingWilderness()
    {
        Assert.That(MapOne(TileWith(ownership: null)).Ownership, Is.Null);
    }

    [Test, Description("A watched Tile nobody holds is mapped as the real Unclaimed record, which is a different answer from the null that means the Player is not being told.")]
    public void ShouldDistinguishWildernessFromNotBeingTold()
    {
        ITileOwnership? wilderness = MapOne(TileWith(TileOwnership.Unclaimed)).Ownership;
        ITileOwnership? notTold = MapOne(TileWith(ownership: null)).Ownership;

        Assert.Multiple(() =>
        {
            Assert.That(wilderness, Is.Not.Null);
            Assert.That(wilderness!.Status, Is.EqualTo(TileOwnershipStatus.Unclaimed));
            Assert.That(notTold, Is.Null);
        });
    }
}
