using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="TerritoryClaimability"/> — no database, because it is three predicates over one tile snapshot.
/// </summary>
/// <remarks>
/// These pin what a Settlement's radius, a Structure's assertion and enclosure absorption all share, so none of the three can end up with its own opinion of what a
/// mountain or a stretch of sea is.
/// </remarks>
[TestFixture]
internal class TerritoryClaimabilityTests
{
    private const int _PLAYER = 1;
    private const int _RIVAL = 2;

    private static TerritoryTestGrid NewGrid()
    {
        return new TerritoryTestGrid(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 5, Height: 5, WrapsHorizontally: false, WrapsVertically: false));
    }

    [Test, Description("A mountain inside a border is plausibly yours, so impassable ground is ownable - it simply carries no supply hop.")]
    public void ShouldAllowOwningAMountain()
    {
        TerritoryTile tile = NewGrid().Mountain(2, 2)[2, 2];

        Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: null, _PLAYER), Is.True);
    }

    [Test, Description("A claimed mountain carries no hop, so it could never be supplied and would lapse the Round it arrived - which is why a mountain is only ever owned.")]
    public void ShouldRefuseClaimingAMountain()
    {
        TerritoryTile tile = NewGrid().Mountain(2, 2)[2, 2];

        Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: null, _PLAYER), Is.False);
    }

    [Test, Description("Nothing can ever hold the Void, so it is refused for both tiers rather than merely being hard to reach.")]
    public void ShouldRefuseTheVoidEntirely()
    {
        TerritoryTile tile = NewGrid().Void(2, 2)[2, 2];

        Assert.Multiple(() =>
        {
            Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: null, _PLAYER), Is.False);
            Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: null, _PLAYER), Is.False);
        });
    }

    [Test, Description("Water is ordinary terrain for ownership, so a coastal Settlement takes the sea beside it exactly as it takes the land.")]
    public void ShouldAllowOwningAndClaimingWater()
    {
        TerritoryTile tile = NewGrid().Water(2, 2)[2, 2];

        Assert.Multiple(() =>
        {
            Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: null, _PLAYER), Is.True);
            Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: null, _PLAYER), Is.True);
        });
    }

    [Test, Description("Ownership is exclusive and earned, so building beside a rival never takes ground they already own.")]
    public void ShouldRefuseARivalsOwnedTile()
    {
        TerritoryTile tile = NewGrid()[2, 2];

        Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: _RIVAL, _PLAYER), Is.False);
    }

    [Test, Description("A rival standing on the ground holds it whoever owns it underneath, so an occupied tile is refused however its columns read.")]
    public void ShouldRefuseATileARivalOccupies()
    {
        TerritoryTile tile = NewGrid().Occupy(2, 2, _RIVAL)[2, 2];

        Assert.Multiple(() =>
        {
            Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: null, _PLAYER), Is.False);
            Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: null, _PLAYER), Is.False);
        });
    }

    [Test, Description("Ownership extinguishes every claim on a tile, so a rival's claim is no obstacle to taking the ground - that is how a border pushes outwards.")]
    public void ShouldAllowOwningATileARivalMerelyClaims()
    {
        TerritoryTile tile = NewGrid().Claim(2, 2, _RIVAL)[2, 2];

        Assert.That(TerritoryClaimability.CanOwn(tile, currentOwnerPlayerId: null, _PLAYER), Is.True);
    }

    [Test, Description("A claim and ownership never coexist on one tile, so owned ground cannot be claimed even by the Player who owns it.")]
    public void ShouldRefuseClaimingOwnedGround()
    {
        TerritoryTile tile = NewGrid()[2, 2];

        Assert.Multiple(() =>
        {
            Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: _PLAYER, _PLAYER), Is.False);
            Assert.That(TerritoryClaimability.CanClaim(tile, currentOwnerPlayerId: _RIVAL, _PLAYER), Is.False);
        });
    }

    [Test, Description("A hop follows passability alone, so a mountain is a dead end in a supply chain while open sea carries one.")]
    public void ShouldCarryAHopOnlyWhereGroundIsPassable()
    {
        TerritoryTestGrid grid = NewGrid().Mountain(1, 1).Water(2, 2);

        Assert.Multiple(() =>
        {
            Assert.That(TerritoryClaimability.CarriesSupplyHop(grid[1, 1]), Is.False);
            Assert.That(TerritoryClaimability.CarriesSupplyHop(grid[2, 2]), Is.True);
            Assert.That(TerritoryClaimability.CarriesSupplyHop(grid[0, 0]), Is.True);
        });
    }
}
