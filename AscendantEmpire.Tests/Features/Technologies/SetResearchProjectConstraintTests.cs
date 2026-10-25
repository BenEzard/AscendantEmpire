using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests that an unlock written by the research-project handler satisfies the database's own rules about status stamps.
/// </summary>
/// <remarks>
/// <para>
/// At the SqlServer tier for one reason: <c>CK_PlayerTechnology_StatusStamps</c> insists that an Unlocked row carry an <c>AcquisitionPaidOnRound</c> and no
/// <c>DiscoveredOnRound</c>, and the in-memory provider enforces no check constraints at all. <see cref="SetResearchProjectHandlerTests"/> covers every other
/// behaviour of the handler more cheaply; this fixture exists so that getting the stamps in the wrong order fails here rather than in a running game.
/// </para>
/// <para>
/// Built on a real game setup rather than hand-seeded rows, so the Player, their research state, the campaign turn queue and the Available statuses are all
/// the genuine article - which is also what makes "pick whatever is Available" a fair way to choose a technology.
/// </para>
/// </remarks>
internal class SetResearchProjectConstraintTests : GameSqlServerTestBase
{
    [Test, Description("Choosing a free technology persists as Unlocked with its Round stamped, which is the shape CK_PlayerTechnology_StatusStamps demands.")]
    public async Task ShouldPersistAnUnlockThatSatisfiesTheStatusStampConstraintAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> setup = await _service.SetupGameAsync(
            new SetupGameRequest(
                GalleryMapId: _galleryIndexId,
                GameName: "Research Project Game",
                NumberOfPlayers: 2,
                BattleMapSize: 50,
                GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setup.HasSucceeded, Is.True, () => string.Join("; ", setup.Errors));

        int gameId = setup.SuccessValue;

        (int playerId, int technologyId, int currentRound) = await ReadFirstAvailableAsync(gameId);

        var handler = new SetResearchProjectHandler(
            NullLogger<SetResearchProjectHandler>.Instance,
            DbFactory,
            new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory));

        // The save is the assertion: a wrong-order stamp would be rejected by the check constraint, not by anything in the handler.
        ServiceResultBase result = await handler.Handle(new SetResearchProjectRequest(gameId, playerId, technologyId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        PlayerTechnology row = await verify.PlayerTechnologies.SingleAsync(candidate => candidate.PlayerId == playerId && candidate.TechnologyId == technologyId);
        PlayerResearchState state = await verify.PlayerResearchStates.SingleAsync(candidate => candidate.PlayerId == playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(TechnologyStatus.Unlocked));
            Assert.That(row.AcquisitionPaidOnRound, Is.EqualTo(currentRound));
            Assert.That(row.DiscoveredOnRound, Is.Null);
            Assert.That(state.CurrentTechnologyId, Is.EqualTo(technologyId));
        }
    }

    /// <summary>
    /// One Player, one of their Available technologies, and the Round the game is on.
    /// </summary>
    /// <remarks>
    /// Whichever technology happens to be Available rather than a named code: the catalogue's six opening technologies are pinned by
    /// <c>StarterTechnologyTests</c> and <c>TechnologySeedDataTests</c>, and naming one here would only couple this fixture to a catalogue it is not about.
    /// </remarks>
    private async Task<(int PlayerId, int TechnologyId, int CurrentRound)> ReadFirstAvailableAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerTechnology available = await context.PlayerTechnologies
            .AsNoTracking()
            .Where(row => row.GameId == gameId && row.Status == TechnologyStatus.Available)
            .OrderBy(row => row.PlayerId)
            .ThenBy(row => row.TechnologyId)
            .FirstAsync();

        int currentRound = await context.TurnQueues
            .AsNoTracking()
            .Where(queue => queue.GameId == gameId && queue.QueueType == Domain.Enums.Turns.TurnQueueType.Campaign)
            .Select(queue => queue.CurrentRoundNumber)
            .SingleAsync();

        return (available.PlayerId, available.TechnologyId, currentRound);
    }
}
