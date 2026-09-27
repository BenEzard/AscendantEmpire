using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Resources;

/// <summary>
/// A seeded resource catalogue, one Player, and a campaign turn queue on a known Round — the setup both the grant and withdraw handler fixtures need.
/// </summary>
/// <remarks>
/// In-memory: both handlers stick to LINQ, Add/Remove and SaveChanges, and every rule they enforce is in the handler rather than in a database constraint. The
/// catalogue is seeded through the real upsert, so the codes the fixtures use are the ones production carries.
/// </remarks>
internal abstract class PlayerResourceUnlockTestBase : InMemoryTestBase
{
    protected const string _RESOURCE_CODE = "Weapons.MeleeWeapons.MedievalSword.Low";
    protected const string _META_CATEGORY_CODE = "Weapons";
    protected const int _CURRENT_ROUND = 7;

    /// <summary>
    /// A Game id used only to find the campaign turn queue. No GameIndex row is needed - the in-memory provider does not enforce the foreign key, and neither
    /// handler reads the Game itself.
    /// </summary>
    protected const int _GAME_ID = 1;

    protected int _playerId;

    [SetUp]
    public async Task SeedCatalogueAndPlayerAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var upsert = new UpsertResourcesHandler(NullLogger<UpsertResourcesHandler>.Instance, context);
        ServiceResultBase seeded = await upsert.Handle(new UpsertResourcesRequest(ResourceSeedData.Build()), CancellationToken.None);
        Assert.That(seeded.HasSucceeded, Is.True, () => string.Join("; ", seeded.Errors));

        var player = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Tester",
            FactionName = "Testers",
            Status = PlayerStatus.Active
        };
        context.Players.Add(player);

        context.TurnQueues.Add(new TurnQueue
        {
            GameId = _GAME_ID,
            QueueType = TurnQueueType.Campaign,
            CurrentRoundNumber = _CURRENT_ROUND
        });

        await context.SaveChangesAsync();
        _playerId = player.Id;
    }

    /// <summary>
    /// Grants the fixture's resource and meta-category, so a withdraw test has something to take away.
    /// </summary>
    protected async Task GrantAsync(int? obsoleteInRounds = null)
    {
        var handler = new GrantPlayerResourceHandler(NullLogger<GrantPlayerResourceHandler>.Instance, DbFactory);

        ServiceResultBase result = await handler.Handle(
            new GrantPlayerResourceRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], [_META_CATEGORY_CODE], obsoleteInRounds),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }
}
