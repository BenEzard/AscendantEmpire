using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests that the database enforces the rules on <c>game.CampaignMapDepletion</c>: no negative amount taken, and one row per real tile.
/// </summary>
/// <remarks>
/// <para>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is
/// restored from the migrated template, so this also proves the migration created them.
/// </para>
/// <para>
/// The Game is built by the real <c>GameSetupService</c>, so its tiles come from the same INSERT that play uses - which also proves that INSERT still works without the
/// columns CM-37 dropped from <c>game.CampaignMap</c>.
/// </para>
/// </remarks>
[TestFixture]
internal class CampaignMapDepletionConstraintTests : GameSqlServerTestBase
{
    private int _gameId;
    private int _tileId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(2, 2, 2);

        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Depletion Game", NumberOfPlayers: _numberOfPlayers, BattleMapSize: 50, GenerationSeed: null);

        ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        _gameId = result.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _tileId = await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();
    }

    [Test, Description("A row recording what has been taken from a real tile saves, so the constraints do not reject the ordinary case.")]
    public async Task ShouldSaveADepletionRowForATileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(BuildDepletion(_tileId, forageTaken: 66_667));
        await context.SaveChangesAsync();

        Assert.That(await context.CampaignMapDepletions.CountAsync(d => d.GameId == _gameId), Is.EqualTo(1));
    }

    [Test, Description("A negative amount taken is rejected, because extraction can never take more than is available and a negative Taken would hand food back to every Player.")]
    public async Task ShouldRejectANegativeAmountTakenAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(BuildDepletion(_tileId, forageTaken: -1));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A row naming a tile that does not exist is rejected, so depletion can never outlive or precede the map it belongs to.")]
    public async Task ShouldRejectARowForATileThatDoesNotExistAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int missingTileId = await context.CampaignMaps.MaxAsync(t => t.CampaignMapId) + 1_000;

        context.CampaignMapDepletions.Add(BuildDepletion(missingTileId, forageTaken: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    private CampaignMapDepletion BuildDepletion(int tileId, long forageTaken)
    {
        return new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _gameId,
            ForageTaken = forageTaken,
            FedOnRound = 1
        };
    }
}
