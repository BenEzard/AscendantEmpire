using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;
using static AscendantEmpire.Tests.Features.Intelligence.IntelligenceReadTestData;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Tests for <see cref="GetPlayerSightingReportsHandler"/>: which reports make up a Player's feed, and that they leave stripped to their fidelity.
/// </summary>
/// <remarks>
/// In-memory, because the read is plain LINQ over the reports, their paths and the Turns. The Game and its Turns are described on <see cref="IntelligenceReadTestData"/>.
/// </remarks>
[TestFixture]
internal class GetPlayerSightingReportsHandlerTests : InMemoryTestBase
{
    private GetPlayerSightingReportsHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await SeedGameAsync(DbFactory);

        _handler = new GetPlayerSightingReportsHandler(
            NullLogger<GetPlayerSightingReportsHandler>.Instance,
            DbFactory,
            new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory),
            new SightDataReader());
    }

    private async Task<IReadOnlyCollection<SightingReport>> ReadFeedAsync(int playerId)
    {
        ServiceResult<IReadOnlyCollection<SightingReport>> result = await _handler.Handle(new GetPlayerSightingReportsRequest(GAME_ID, playerId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    [Test, Description("A Player mid-Turn is shown what was raised since their previous Turn ended, not since the open one began, or everything from the Round's earlier Turns would vanish.")]
    public async Task ShouldReturnOnlyReportsRaisedSinceTheReadersLastTurnEndedAsync()
    {
        await AddAsync(
            DbFactory,
            Report(101, READER_PLAYER_ID, ROUND_ONE_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1)),
            Report(102, READER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1), (5, 1), (6, 2)));

        SightingReport report = (await ReadFeedAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportId, Is.EqualTo(102), "Report 101 was raised before the reader's Round 1 Turn ended, so it has already been seen.");
            Assert.That(report.RoundNumber, Is.EqualTo(2));
            Assert.That(report.Path.Select(coord => (coord.X, coord.Y)), Is.EqualTo(new[] { (4, 1), (5, 1), (6, 2) }), "Tile ids are placed with the Game's geometry, in path order.");
            Assert.That(report.ExitDirection, Is.EqualTo(MoveDirection.East));
        }
    }

    [Test, Description("A Player who has never finished a Turn has no earlier window to measure from, so everything raised for them so far is new.")]
    public async Task ShouldReturnEveryReportWhenTheReaderHasNeverFinishedATurnAsync()
    {
        await AddAsync(
            DbFactory,
            Report(202, OTHER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1)),
            Report(201, OTHER_PLAYER_ID, ROUND_ONE_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1)));

        IReadOnlyCollection<SightingReport> reports = await ReadFeedAsync(OTHER_PLAYER_ID);

        Assert.That(reports.Select(report => report.ReportId), Is.EqualTo(new long[] { 201, 202 }), "Oldest Turn first.");
    }

    [Test, Description("A Player's feed holds only reports addressed to them, because another Player's intelligence is exactly what the feed must never hand over.")]
    public async Task ShouldNeverReturnAnotherPlayersReportsAsync()
    {
        await AddAsync(
            DbFactory,
            Report(301, OTHER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1)),
            Report(302, READER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Detail, (4, 1)));

        IReadOnlyCollection<SightingReport> reports = await ReadFeedAsync(READER_PLAYER_ID);

        Assert.That(reports.Select(report => report.ReportId), Is.EqualTo(new long[] { 302 }));
    }

    [Test, Description("The feed goes through the redaction layer, so a report earned at Movement fidelity reaches the Player without the identity stored beside it.")]
    public async Task ShouldStripReportsDownToTheirFidelityAsync()
    {
        await AddAsync(DbFactory, Report(401, READER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Movement, (4, 1)));

        SightingReport report = (await ReadFeedAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnitInstanceId, Is.Null);
            Assert.That(report.OwnerPlayerId, Is.Null);
            Assert.That(report.UnitTemplateId, Is.Null);
            Assert.That(report.HealthyTroops, Is.Null);
        }
    }

    [Test, Description("A Structure report reaches the feed on its one tile, with its Structure, owners, progress and the events seen, since a Structure has no path of its own.")]
    public async Task ShouldReturnAStructureReportOnItsTileWithWhatWasSeenAsync()
    {
        await AddAsync(DbFactory, StructureReport(501, READER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Detail, (6, 3)));

        SightingReport report = (await ReadFeedAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportType, Is.EqualTo(Domain.Enums.Intelligence.PlayerReportType.StructureSighted));
            Assert.That(report.Path.Select(coord => (coord.X, coord.Y)), Is.EqualTo(new[] { (6, 3) }), "The first and last tile are the same, so the path is that one tile.");
            Assert.That(report.StructureInstanceId, Is.EqualTo(60));
            Assert.That(report.StructureId, Is.EqualTo(7));
            Assert.That(report.OwnerPlayerId, Is.EqualTo(ACTING_PLAYER_ID));
            Assert.That(report.PreviousOwnerPlayerId, Is.EqualTo(READER_PLAYER_ID));
            Assert.That(report.CompletionPercent, Is.EqualTo(80));
            Assert.That(report.CapturedInView, Is.True);
            Assert.That(report.DestroyedInView, Is.True);
            Assert.That(report.CompletedInView, Is.False);
            Assert.That(report.UnitInstanceId, Is.Null, "A Structure report has no Force subject.");
        }
    }

    [Test, Description("A Structure report goes through the redaction layer too, so one earned at Type names the kind of Structure but not whose it was.")]
    public async Task ShouldStripAStructureReportDownToItsFidelityAsync()
    {
        await AddAsync(DbFactory, StructureReport(502, READER_PLAYER_ID, ROUND_TWO_ACTING_TURN_ID, SightingFidelity.Type, (6, 3)));

        SightingReport report = (await ReadFeedAsync(READER_PLAYER_ID)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.StructureId, Is.EqualTo(7));
            Assert.That(report.OwnerPlayerId, Is.Null);
            Assert.That(report.PreviousOwnerPlayerId, Is.Null);
            Assert.That(report.StructureInstanceId, Is.Null);
            Assert.That(report.CompletionPercent, Is.Null);
        }
    }

    [Test, Description("A Player that is not in the Game is a request for nothing, and is answered as not found rather than with an empty feed that looks like a quiet Round.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGameAsync()
    {
        ServiceResult<IReadOnlyCollection<SightingReport>> result = await _handler.Handle(new GetPlayerSightingReportsRequest(GAME_ID, OUTSIDER_PLAYER_ID), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }
}
