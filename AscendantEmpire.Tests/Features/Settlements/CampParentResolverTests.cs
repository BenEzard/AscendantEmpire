using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Settlements.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Settlements;

/// <summary>
/// Tests for <see cref="CampParentResolver"/>: which settlement a new camp is given to feed, and which candidates are not offered at all.
/// </summary>
/// <remarks>
/// In-memory, per <c>testing.md</c>: the resolver writes nothing, but it still runs three LINQ reads, so a fixture has to be able to seed rows for it to read. The Game
/// row carries nothing but the map's shape, so the distances a test arranges are the ones the rule is judged on rather than whatever a fully seeded game happens to
/// measure.
/// </remarks>
[TestFixture]
internal class CampParentResolverTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 7;
    private const int _OTHER_PLAYER_ID = 8;

    private const int _MAP_WIDTH = 8;
    private const int _MAP_HEIGHT = 8;
    private const int _MAP_OFFSET_ID = 1_000;

    /// <summary>The structure the camp under test stands on, which is the tile every distance is measured from.</summary>
    private const int _CAMP_STRUCTURE_ID = 100;

    private const int _NEAR_SETTLEMENT_ID = 10;
    private const int _FAR_SETTLEMENT_ID = 20;

    private CampParentResolver _resolver = null!;

    [SetUp]
    public void BuildResolver()
    {
        _resolver = new CampParentResolver(NullLogger<CampParentResolver>.Instance);
    }

    [Test, Description("A camp founded with one village standing is given it to feed, which is the ordinary case camp deliveries exist for.")]
    public async Task ShouldResolveTheOnlyCandidate()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 3, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_NEAR_SETTLEMENT_ID));
    }

    [Test, Description("The nearer of two candidates wins, because a camp feeds the settlement it is closest to rather than the first one founded.")]
    public async Task ShouldResolveTheNearerCandidate()
    {
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 5, y: 0);
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 2, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_NEAR_SETTLEMENT_ID));
    }

    [Test, Description("Two equidistant candidates are separated by the lower settlement id, so the same founding always resolves to the same parent.")]
    public async Task ShouldBreakATieOnTheLowerSettlementId()
    {
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 0, y: 2);
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 2, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_NEAR_SETTLEMENT_ID));
    }

    [Test, Description("A village the Player does not hold is not a candidate, because a camp can only share a network with its own Player's banks.")]
    public async Task ShouldIgnoreAnotherPlayersSettlement()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 1, y: 0, ownerPlayerId: _OTHER_PLAYER_ID);
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 5, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_FAR_SETTLEMENT_ID));
    }

    [Test, Description("A razed settlement is not a candidate, since what a camp feeds has to still be standing to receive anything.")]
    public async Task ShouldIgnoreARazedSettlement()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 1, y: 0, razed: true);
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 5, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_FAR_SETTLEMENT_ID));
    }

    [Test, Description("Another camp is not a candidate, because delivering a small store into another small store would gain the Player nothing.")]
    public async Task ShouldIgnoreAnotherCamp()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 1, y: 0, tier: SettlementTier.Camp);
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 5, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_FAR_SETTLEMENT_ID));
    }

    [Test, Description("A grown settlement above village is a candidate, so growth does not have to come back and widen this rule.")]
    public async Task ShouldAcceptATierAboveVillage()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 2, y: 0, tier: SettlementTier.Town);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.EqualTo(_NEAR_SETTLEMENT_ID));
    }

    [Test, Description("The first camp of a game resolves to no parent and succeeds, because the opening settlement is a camp with nothing above it to attach to.")]
    public async Task ShouldResolveNoParentWhenThereIsNoCandidate()
    {
        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0);

        Assert.That(parentSettlementId, Is.Null);
    }

    [Test, Description("Distance takes a wrapping edge, so a settlement round the short way is nearer than one the long way across the map.")]
    public async Task ShouldMeasureDistanceAcrossAWrappingEdge()
    {
        await SeedCandidateAsync(_NEAR_SETTLEMENT_ID, x: 7, y: 0);
        await SeedCandidateAsync(_FAR_SETTLEMENT_ID, x: 3, y: 0);

        int? parentSettlementId = await ResolveAsync(campX: 0, campY: 0, wrapsHorizontally: true);

        Assert.That(parentSettlementId, Is.EqualTo(_NEAR_SETTLEMENT_ID), "One step round the joined edge beats three across the middle.");
    }

    /// <summary>
    /// Stages the structure the camp stands on and resolves the parent for it, asserting the call itself succeeded.
    /// </summary>
    /// <remarks>
    /// No camp Settlement row is seeded, because the resolver never reads one: it takes the structure id and measures from that tile. That is also why the real call site
    /// can resolve before the founding it is part of has saved anything.
    /// </remarks>
    private async Task<int?> ResolveAsync(int campX, int campY, bool wrapsHorizontally = false)
    {
        await SeedMapShapeAsync(wrapsHorizontally);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.Add(BuildStructure(_CAMP_STRUCTURE_ID, campX, campY, _PLAYER_ID, razed: false));
        await context.SaveChangesAsync();

        ServiceResult<int?> result = await _resolver.ResolveAsync(context, _GAME_ID, _PLAYER_ID, _CAMP_STRUCTURE_ID, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    /// <summary>A standing settlement of the given tier at the given coordinate, with the structure that carries its ownership and its tile.</summary>
    /// <remarks>The structure id is the settlement id, so a fixture reading an assertion can tell at a glance which row the resolver picked.</remarks>
    private async Task SeedCandidateAsync(
        int settlementId,
        int x,
        int y,
        SettlementTier tier = SettlementTier.Village,
        int? ownerPlayerId = null,
        bool razed = false)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.Add(BuildStructure(settlementId, x, y, ownerPlayerId ?? _PLAYER_ID, razed));

        context.Settlements.Add(new Settlement
        {
            Id = settlementId,
            GameId = _GAME_ID,
            StructureInstanceId = settlementId,
            FoundingPlayerId = ownerPlayerId ?? _PLAYER_ID,
            Name = tier == SettlementTier.Camp ? null : $"Settlement {settlementId}",
            Tier = tier,
            Population = 100,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 1
        });

        await context.SaveChangesAsync();
    }

    private static StructureInstance BuildStructure(int structureInstanceId, int x, int y, int ownerPlayerId, bool razed)
    {
        return new StructureInstance
        {
            Id = structureInstanceId,
            GameId = _GAME_ID,
            CampaignMapTileId = _MAP_OFFSET_ID + x + (y * _MAP_WIDTH),
            BattleMapTileId = null,
            StructureId = 1,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = ownerPlayerId,
            CreatedOnTurnId = 1,
            BuildPointsProgress = 0,
            BuildPointsPerTurn = 0,
            RemovedOnTurnId = razed ? 1 : null
        };
    }

    /// <summary>
    /// The Game row the resolver reads the board's shape off.
    /// </summary>
    /// <remarks>Only the geometry columns matter, so the rest are the smallest values that satisfy the entity's required members.</remarks>
    private async Task SeedMapShapeAsync(bool wrapsHorizontally = false, bool wrapsVertically = false)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.GameIndex.Add(new GameIndex
        {
            Id = _GAME_ID,
            CampaignMapIndexId = 1,
            GameName = "Camp Parent Test",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 50,
            CampaignMapOffsetId = _MAP_OFFSET_ID,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            HorizontalWrap = wrapsHorizontally,
            VerticalWrap = wrapsVertically
        });

        await context.SaveChangesAsync();
    }
}
