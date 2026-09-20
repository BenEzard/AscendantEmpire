using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Units.Services;

/// <summary>
/// Tests for <see cref="StarvationUnderfedForceResolver"/>: what it writes to a Force that went short of food, and what it leaves alone.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The resolver reads and writes through LINQ and the change tracker only; the lifecycle writer is substituted, both because
/// destroying a Force is its business rather than this one's and because the real writer needs an open Turn to allocate a sequence from.
/// </para>
/// <para>
/// The arithmetic itself is pinned without a database in <c>StarvationCasualtyCalculatorTests</c>. What is tested here is the reading, the writing, and the
/// reproducibility the derived seed exists to give.
/// </para>
/// <para>
/// Severity is 34% at total starvation, so a Force that gets no food at all loses exactly 34% of its troops to death and injury together. How that total
/// splits is rolled, so the assertions below pin the figures that do not depend on the roll and bound the ones that do.
/// </para>
/// </remarks>
[TestFixture]
internal class StarvationUnderfedForceResolverTests : InMemoryTestBase
{
    private const int _PLAYER_ID = 3;
    private const int _TILE_ID = 501;
    private const int _ROUND_NUMBER = 6;
    private const int _PLAYER_TURN_ID = 88;

    /// <summary>34% of 1,000 troops: the total a wholly unfed Force of that size loses, dead and wounded together.</summary>
    private const int _CASUALTIES_OF_A_THOUSAND = 340;

    private int _gameId;
    private IForceLifecycleWriter _forceLifecycleWriter = null!;
    private StarvationUnderfedForceResolver _resolver = null!;

    [SetUp]
    public async Task SetUpResolverAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            var game = new GameIndex
            {
                CampaignMapIndexId = 1,
                GameName = "Starvation Tests",
                NumberOfPlayers = 2,
                CreationStatus = GameCreationStatus.SetupComplete,
                PlayStatus = GamePlayStatus.InProgress,
                CreatedAt = DateTimeOffset.UtcNow,
                GenerationSeed = 987654321,
                BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
                MapWidth = 10,
                MapHeight = 10,
                CampaignMapOffsetId = 0
            };

            context.GameIndex.Add(game);
            await context.SaveChangesAsync();

            _gameId = game.Id;
        }

        _forceLifecycleWriter = Substitute.For<IForceLifecycleWriter>();

        _forceLifecycleWriter
            .DestroyAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<UnitInstance>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.Succeed());

        _resolver = new StarvationUnderfedForceResolver(_forceLifecycleWriter, NullLogger<StarvationUnderfedForceResolver>.Instance);
    }

    [Test, Description("A wholly unfed Force loses 34% of its troops, leaving the rest of its healthy men untouched whichever way the dead/wounded roll falls.")]
    public async Task ShouldTakeCasualtiesInProportionToTheShortfallAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);

        UnitInstance force = await ResolveAndReloadAsync(unitInstanceId: 1, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(force.HealthyCount, Is.EqualTo(1_000 - _CASUALTIES_OF_A_THOUSAND), "Every casualty comes out of the healthy: some died, the rest were wounded.");
            Assert.That(force.HealthyCount + force.InjuredCount, Is.LessThan(1_000), "Some of them died.");
            Assert.That(force.InjuredCount, Is.LessThan(_CASUALTIES_OF_A_THOUSAND), "And so fewer than all the casualties were merely wounded.");
        }
    }

    [Test, Description("A Force that fed in full is left exactly as it was, so being handed every Force gathering fed costs the well-fed nothing.")]
    public async Task ShouldLeaveAWellFedForceAloneAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 500, injuredCount: 20);

        var fedInFull = new ForceFeedingResult(1, _TILE_ID, ScaledAmount.FromWhole(520), ScaledAmount.FromWhole(520), ScaledAmount.Zero);

        UnitInstance force = await ResolveAndReloadAsync(unitInstanceId: 1, fedInFull);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(force.HealthyCount, Is.EqualTo(500));
            Assert.That(force.InjuredCount, Is.EqualTo(20));
        }
    }

    [Test, Description("A wholly wounded Force loses its casualties outright, because a wound with nobody healthy left to take it kills instead.")]
    public async Task ShouldKillWhenTheForceIsAlreadyWhollyWoundedAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 0, injuredCount: 1_000);

        UnitInstance force = await ResolveAndReloadAsync(unitInstanceId: 1, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(force.HealthyCount, Is.Zero);
            Assert.That(force.InjuredCount, Is.EqualTo(1_000 - _CASUALTIES_OF_A_THOUSAND));
        }
    }

    [Test, Description("The casualties are staged on the caller's context and not saved, so they commit in gathering's own save alongside the depletion.")]
    public async Task ShouldStageTheCasualtiesWithoutSavingAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await ResolveAsync(context, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        await using AscendantEmpireContext reader = await DbFactory.CreateDbContextAsync();
        UnitInstance unsaved = await reader.UnitInstances.SingleAsync(force => force.Id == 1);

        Assert.That(unsaved.HealthyCount, Is.EqualTo(1_000), "Nothing reached the database until the caller saved.");
    }

    [Test, Description("The same Round resolved twice produces identical casualties, so re-running the non-transactional end-of-Turn pipeline is not a fresh roll.")]
    public async Task ShouldProduceTheSameCasualtiesOnARerunAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);
        UnitInstance firstRun = await ResolveAndReloadAsync(unitInstanceId: 1, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        await ResetForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);
        UnitInstance secondRun = await ResolveAndReloadAsync(unitInstanceId: 1, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondRun.HealthyCount, Is.EqualTo(firstRun.HealthyCount));
            Assert.That(secondRun.InjuredCount, Is.EqualTo(firstRun.InjuredCount));
        }
    }

    [Test, Description("Two Forces going equally short are damaged independently, so neither the processing order nor one another's luck decides their losses.")]
    public async Task ShouldRollSeparatelyForEachForceAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);
        await AddForceAsync(unitInstanceId: 2, healthyCount: 1_000, injuredCount: 0);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await ResolveAsync(context, WhollyUnfed(unitInstanceId: 1, troops: 1_000), WhollyUnfed(unitInstanceId: 2, troops: 1_000));
            await context.SaveChangesAsync();
        }

        await using AscendantEmpireContext reader = await DbFactory.CreateDbContextAsync();
        List<UnitInstance> forces = await reader.UnitInstances.OrderBy(force => force.Id).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forces[0].HealthyCount, Is.EqualTo(1_000 - _CASUALTIES_OF_A_THOUSAND), "Both lost the same total, which the shortfall alone decides.");
            Assert.That(forces[1].HealthyCount, Is.EqualTo(1_000 - _CASUALTIES_OF_A_THOUSAND));
        }
    }

    [Test, Description("A Force starvation cannot empty is never handed to the lifecycle writer, so nothing is destroyed while it still has troops.")]
    public async Task ShouldNotDestroyAForceThatStillHasTroopsAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);

        await ResolveAndReloadAsync(unitInstanceId: 1, WhollyUnfed(unitInstanceId: 1, troops: 1_000));

        await _forceLifecycleWriter.DidNotReceiveWithAnyArgs().DestroyAsync(default!, default!, default, default);
    }

    [Test, Description("With no Game row there is no seed to roll against, so no Force is damaged rather than being damaged against an invented one.")]
    public async Task ShouldTakeNoCasualtiesWhenTheGameIsMissingAsync()
    {
        await AddForceAsync(unitInstanceId: 1, healthyCount: 1_000, injuredCount: 0);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _resolver.ResolveAsync(
            context,
            gameId: _gameId + 500,
            playerId: _PLAYER_ID,
            roundNumber: _ROUND_NUMBER,
            playerTurnId: _PLAYER_TURN_ID,
            results: [WhollyUnfed(unitInstanceId: 1, troops: 1_000)]);

        await context.SaveChangesAsync();

        await using AscendantEmpireContext reader = await DbFactory.CreateDbContextAsync();
        UnitInstance force = await reader.UnitInstances.SingleAsync(unit => unit.Id == 1);

        Assert.That(force.HealthyCount, Is.EqualTo(1_000));
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    /// <summary>A Force that fed on the tile and got none of what it needed, at 1 troop to 1 food.</summary>
    private static ForceFeedingResult WhollyUnfed(int unitInstanceId, int troops)
    {
        ScaledAmount need = ScaledAmount.FromWhole(troops);

        return new ForceFeedingResult(unitInstanceId, _TILE_ID, need, ScaledAmount.Zero, need);
    }

    private Task ResolveAsync(AscendantEmpireContext context, params ForceFeedingResult[] results)
    {
        return _resolver.ResolveAsync(
            context,
            gameId: _gameId,
            playerId: _PLAYER_ID,
            roundNumber: _ROUND_NUMBER,
            playerTurnId: _PLAYER_TURN_ID,
            results: results);
    }

    /// <summary>Resolves on a context of its own, saves the way gathering would, and reads the Force back.</summary>
    private async Task<UnitInstance> ResolveAndReloadAsync(int unitInstanceId, params ForceFeedingResult[] results)
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await ResolveAsync(context, results);
            await context.SaveChangesAsync();
        }

        await using AscendantEmpireContext reader = await DbFactory.CreateDbContextAsync();

        return await reader.UnitInstances.SingleAsync(force => force.Id == unitInstanceId);
    }

    private async Task AddForceAsync(int unitInstanceId, int healthyCount, int injuredCount)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _gameId,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = _TILE_ID,
            UnitTemplateId = 1,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Puts a Force back to full strength so the same Round can be resolved against it a second time.</summary>
    private async Task ResetForceAsync(int unitInstanceId, int healthyCount, int injuredCount)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance force = await context.UnitInstances.SingleAsync(unit => unit.Id == unitInstanceId);

        force.HealthyCount = healthyCount;
        force.InjuredCount = injuredCount;

        await context.SaveChangesAsync();
    }
}
