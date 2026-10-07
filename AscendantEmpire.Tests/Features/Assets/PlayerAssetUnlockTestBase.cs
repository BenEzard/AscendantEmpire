using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// A seeded asset catalogue, one Player, and a campaign turn queue on a known Round — the setup both the grant and withdraw handler fixtures need.
/// </summary>
/// <remarks>
/// In-memory: both handlers stick to LINQ, Add/Remove and SaveChanges, and every rule they enforce is in the handler rather than in a database constraint. The
/// catalogue is seeded through the real upsert, so the codes the fixtures use are the ones production carries.
/// </remarks>
internal abstract class PlayerAssetUnlockTestBase : InMemoryTestBase
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

        var upsert = new UpsertAssetsHandler(NullLogger<UpsertAssetsHandler>.Instance, context);
        ServiceResultBase seeded = await upsert.Handle(new UpsertAssetsRequest(AssetSeedData.Build()), CancellationToken.None);
        Assert.That(seeded.HasSucceeded, Is.True, () => string.Join("; ", seeded.Errors));

        var player = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Tester",
            FactionName = "Testers",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png",
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
    /// Grants the fixture's asset and meta-category, so a withdraw test has something to take away.
    /// </summary>
    protected Task GrantAsync(int? obsoleteInRounds = null)
    {
        return GrantAsync([_RESOURCE_CODE], [_META_CATEGORY_CODE], obsoleteInRounds);
    }

    /// <summary>
    /// Grants the named catalogue rows through the real handler, so the unlock rows a test reads are the ones production writes.
    /// </summary>
    /// <remarks>
    /// Either list may be empty, but not both. Granting the two separately is what lets a test obsolete a meta-category while leaving the Assets
    /// beneath it current, since one call stamps the same Round onto everything it names.
    /// </remarks>
    /// <param name="obsoleteInRounds">Offset from the current Round. Negative or zero puts the obsoleting Round in the past or on it.</param>
    protected async Task GrantAsync(
        IReadOnlyCollection<string> assetCodes,
        IReadOnlyCollection<string> metaCategoryCodes,
        int? obsoleteInRounds = null)
    {
        var handler = new GrantPlayerAssetHandler(NullLogger<GrantPlayerAssetHandler>.Instance, DbFactory);

        ServiceResultBase result = await handler.Handle(
            new GrantPlayerAssetRequest(_GAME_ID, _playerId, assetCodes, metaCategoryCodes, obsoleteInRounds),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }
}
