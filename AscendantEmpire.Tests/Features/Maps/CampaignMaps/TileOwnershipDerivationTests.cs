using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="TileOwnershipDerivation"/>: the precedence between the ownership layers, and the per-viewer claim flags.
/// </summary>
/// <remarks>
/// <para>
/// On the no-database tier. The rule is pure - it takes a tile's columns, its claimants and whether a battle is on it, and gives back a state - so nothing here needs a
/// context, let alone a container.
/// </para>
/// <para>
/// Two Players throughout, 1 and 2. What the states <i>mean</i> is in <c>docs/design/campaign-map/tile-ownership.md</c>; what is pinned here is which one wins when several
/// could apply, and that the claimants come back named and in a stable order, because the client draws them in it.
/// </para>
/// </remarks>
[TestFixture]
internal class TileOwnershipDerivationTests
{
    private const int _TILE_ID = 100;

    private const int _ROME = 1;
    private const int _CARTHAGE = 2;

    /// <summary>A tile's columns with everything unset but whatever the test overrides.</summary>
    private static TileOwnershipColumns Columns(
        int? ownedByPlayerId = null,
        int? occupiedByPlayerId = null,
        int? lastOwnedByPlayerId = null,
        int? rebellionPercentRemaining = null,
        bool hasClaims = false)
    {
        return new TileOwnershipColumns
        {
            CampaignMapId = _TILE_ID,
            OwnedByPlayerId = ownedByPlayerId,
            OccupiedByPlayerId = occupiedByPlayerId,
            LastOwnedByPlayerId = lastOwnedByPlayerId,
            RebellionPercentRemaining = rebellionPercentRemaining,
            HasClaims = hasClaims
        };
    }

    /// <summary>Resolves a tile with no claims and no battle unless the test says otherwise.</summary>
    private static TileOwnership Derive(TileOwnershipColumns columns, int[]? claimantPlayerIds = null, bool hasBattle = false)
    {
        return TileOwnershipDerivation.Derive(columns, claimantPlayerIds ?? [], hasBattle: hasBattle);
    }

    [Test, Description("A tile with nothing on any ownership column and no claims resolves as wilderness, which most of a map is.")]
    public void ShouldResolveUnclaimedWhenNothingHoldsTheTile()
    {
        TileOwnership ownership = Derive(Columns());

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Unclaimed));
            Assert.That(ownership.EffectiveHolderPlayerId, Is.Null);
            Assert.That(ownership.ClaimedByPlayerIds, Is.Empty);
        });
    }

    [Test, Description("An owner on the tile and nothing else resolves as Owned, and the owner is the effective holder.")]
    public void ShouldResolveOwnedWhenOnlyAnOwnerIsSet()
    {
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME));

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Owned));
            Assert.That(ownership.OwnedByPlayerId, Is.EqualTo(_ROME));
            Assert.That(ownership.EffectiveHolderPlayerId, Is.EqualTo(_ROME));
        });
    }

    [Test, Description("An occupier outranks the owner it is standing on, and the owner keeps ownership throughout rather than being replaced by it.")]
    public void ShouldResolveOccupiedAndKeepTheOwnerWhenBothAreSet()
    {
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME, occupiedByPlayerId: _CARTHAGE));

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Occupied));
            // Both layers are reported: occupation changes who collects the yield, never who owns the ground.
            Assert.That(ownership.OwnedByPlayerId, Is.EqualTo(_ROME));
            Assert.That(ownership.OccupiedByPlayerId, Is.EqualTo(_CARTHAGE));
            // The holder is the occupier, which is the whole point of the fallback.
            Assert.That(ownership.EffectiveHolderPlayerId, Is.EqualTo(_CARTHAGE));
        });
    }

    [Test, Description("A battle on the tile resolves as Disputed whoever owns or occupies it, since nobody collects the yield while they fight.")]
    public void ShouldResolveDisputedWhenABattleIsOnTheTileHoweverItIsHeld()
    {
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME, occupiedByPlayerId: _CARTHAGE), hasBattle: true);

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Disputed));
            // Still reported, because the fighting does not change who held the ground under it.
            Assert.That(ownership.OwnedByPlayerId, Is.EqualTo(_ROME));
        });
    }

    [Test, Description("A former owner with no current one resolves as Abandoned, which is what a razed Settlement's tiles become.")]
    public void ShouldResolveAbandonedWhenOnlyAFormerOwnerIsSet()
    {
        TileOwnership ownership = Derive(Columns(lastOwnedByPlayerId: _CARTHAGE));

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Abandoned));
            Assert.That(ownership.LastOwnedByPlayerId, Is.EqualTo(_CARTHAGE));
            // Nobody holds an abandoned tile, which is what distinguishes it from an owned one.
            Assert.That(ownership.EffectiveHolderPlayerId, Is.Null);
        });
    }

    [Test, Description("Ownership trumps claim: a tile that is both owned and carrying claim rows resolves as Owned rather than Claimed.")]
    public void ShouldResolveOwnedRatherThanClaimedWhenBothApply()
    {
        // Ownership extinguishes every rival claim on a tile, so in a correct database these never coexist. Pinned anyway, because the resolution must not depend on that.
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME, hasClaims: true), claimantPlayerIds: [_CARTHAGE]);

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Owned));
            // The claim is still reported, so a client is never told less than it was told before.
            Assert.That(ownership.ClaimedByPlayerIds, Is.EquivalentTo(new[] { _CARTHAGE }));
        });
    }

    [Test, Description("An unowned tile with claim rows resolves as Claimed and names its claimant, which is the only non-exclusive state.")]
    public void ShouldResolveClaimedWhenAnUnownedTileCarriesClaims()
    {
        TileOwnership ownership = Derive(Columns(hasClaims: true), claimantPlayerIds: [_ROME]);

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Claimed));
            Assert.That(ownership.ClaimedByPlayerIds, Is.EquivalentTo(new[] { _ROME }));
        });
    }

    [Test, Description("Every claimant of a tile is named, because claims are non-exclusive and the client draws one colour per claimant.")]
    public void ShouldNameEveryClaimantOfATileSeveralPlayersAssert()
    {
        TileOwnership ownership = Derive(Columns(hasClaims: true), claimantPlayerIds: [_ROME, _CARTHAGE]);

        Assert.That(ownership.ClaimedByPlayerIds, Is.EquivalentTo(new[] { _ROME, _CARTHAGE }));
    }

    [Test, Description("The claimants come back in Player id order whatever order the rows arrived in, since that order is drawn and listed to the Player.")]
    public void ShouldOrderTheClaimantsByPlayerIdWhateverOrderTheRowsArriveIn()
    {
        TileOwnership ownership = Derive(Columns(hasClaims: true), claimantPlayerIds: [_CARTHAGE, _ROME]);

        // Ordered, not merely equivalent: the border alternates these colours along its dashes and the tooltip lists them to
        // match, so a shifting order would make the dashes swap colours between reads for no reason.
        Assert.That(ownership.ClaimedByPlayerIds, Is.EqualTo(new[] { _ROME, _CARTHAGE }));
    }

    [Test, Description("The claim rows decide the state, not HasClaims: a flag left set over rows that have gone resolves as unclaimed rather than claimed by nobody.")]
    public void ShouldResolveUnclaimedWhenTheClaimFlagIsSetButNoRowsRemain()
    {
        TileOwnership ownership = Derive(Columns(hasClaims: true), claimantPlayerIds: []);

        Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Unclaimed));
    }

    [Test, Description("Rebellion is a modifier on Owned rather than a state, so a rebelling tile still resolves as Owned with the flag set beside it.")]
    public void ShouldFlagRebellionWithoutChangingTheStatus()
    {
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME, rebellionPercentRemaining: 40));

        Assert.Multiple(() =>
        {
            Assert.That(ownership.Status, Is.EqualTo(TileOwnershipStatus.Owned));
            Assert.That(ownership.IsRebelling, Is.True);
        });
    }

    [Test, Description("A tile that has finished rebelling has its column nulled out rather than left at 0, so 'is rebelling' stays a null check.")]
    public void ShouldNotFlagRebellionWhenTheColumnIsNull()
    {
        TileOwnership ownership = Derive(Columns(ownedByPlayerId: _ROME));

        Assert.That(ownership.IsRebelling, Is.False);
    }
}
