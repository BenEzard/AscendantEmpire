using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Handlers;

public record GetPlayerSightingReportsRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<SightingReport>>>;

internal class GetPlayerSightingReportsRequestValidator : Validator<GetPlayerSightingReportsRequest>
{
    public GetPlayerSightingReportsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// A Player's intelligence feed: the reports raised for them since their last Turn ended, stripped to the fidelity each earned.
/// </summary>
/// <remarks>
/// <para>
/// There is no read flag. The window is derived from Turn identity instead - see <see cref="ICampaignTurnService.GetLastEndedOwnTurnIdAsync"/> - so a Player who reads the feed, leaves and comes
/// back mid-Round sees the same list again, which the design accepts.
/// </para>
/// <para>
/// Every report goes through <see cref="SightingDisclosure"/> before it leaves. Tile ids are placed with the Game's geometry rather than by joining the map table.
/// </para>
/// </remarks>
internal class GetPlayerSightingReportsHandler(
    ILogger<GetPlayerSightingReportsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTurnService campaignTurnService,
    ISightDataReader sightDataReader
) : IRequestHandler<GetPlayerSightingReportsRequest, ServiceResult<IReadOnlyCollection<SightingReport>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<SightingReport>>> Handle(GetPlayerSightingReportsRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetPlayerSightingReportsRequestValidator();
            ValidationResult validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayerSightingReportsRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<IReadOnlyCollection<SightingReport>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The token already ties the caller to this Player, but a Player that is not in the Game is a request for nothing, and says so rather than answering with an empty feed.
            bool isPlayerInGame = await context.Players.AnyAsync(player => player.Id == request.PlayerId && player.GameId == request.GameId, cancelToken);

            if (isPlayerInGame == false)
            {
                string error = $"Player Id {request.PlayerId} was not found in Game Id {request.GameId}.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<IReadOnlyCollection<SightingReport>>.NotFound(error);
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, request.GameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {request.GameId} was not found while reading sighting reports.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<IReadOnlyCollection<SightingReport>>.NotFound(error);
            }

            int? lastEndedOwnTurnId = await campaignTurnService.GetLastEndedOwnTurnIdAsync(context, request.GameId, request.PlayerId, cancelToken);

            IQueryable<PlayerReport> recipientReports = context.PlayerReports
                .AsNoTracking()
                .Where(report => report.GameId == request.GameId && report.RecipientPlayerId == request.PlayerId);

            // A Player who has never finished a Turn sees everything raised for them so far.
            if (lastEndedOwnTurnId is int lastEndedTurnId)
            {
                recipientReports = recipientReports.Where(report => report.RaisedDuringPlayerTurnId > lastEndedTurnId);
            }

            var reportRows = await (from report in recipientReports
                                    join turn in context.PlayerTurns.AsNoTracking()
                                        on report.RaisedDuringPlayerTurnId equals turn.Id
                                    orderby report.RaisedDuringPlayerTurnId, report.FirstTurnSequence, report.Id
                                    select new { Report = report, turn.RoundNumber }).ToListAsync(cancelToken);

            if (reportRows.Count == 0)
            {
                return ServiceResult<IReadOnlyCollection<SightingReport>>.Succeed([]);
            }

            long[] reportIds = [.. reportRows.Select(row => row.Report.Id)];

            // The paths, read separately rather than through an Include so the report query stays one flat row per report.
            Dictionary<long, List<Coord>> pathByReportId = (await context.PlayerReportPathTiles
                    .AsNoTracking()
                    .Where(tile => reportIds.Contains(tile.PlayerReportId))
                    .OrderBy(tile => tile.PlayerReportId)
                    .ThenBy(tile => tile.Ordinal)
                    .ToListAsync(cancelToken))
                .GroupBy(tile => tile.PlayerReportId)
                .ToDictionary(group => group.Key, group => group.Select(tile => geometry.GetCoord(tile.CampaignMapTileId)).ToList());

            List<SightingReport> disclosedReports = [];

            foreach (var row in reportRows)
            {
                PlayerReport report = row.Report;

                // A report whose path rows are missing still has its first and last tile on the row itself, so it degrades to those rather than to nothing.
                IReadOnlyList<Coord> path = pathByReportId.TryGetValue(report.Id, out List<Coord>? pathCoords) && pathCoords.Count > 0
                    ? pathCoords
                    : [.. new[] { report.FirstCampaignMapTileId, report.LastCampaignMapTileId }.Distinct().Select(geometry.GetCoord)];

                disclosedReports.Add(SightingDisclosure.Disclose(new StoredSightingReport
                {
                    ReportId = report.Id,
                    ReportType = report.ReportType,
                    RaisedDuringPlayerTurnId = report.RaisedDuringPlayerTurnId,
                    RoundNumber = row.RoundNumber,
                    Fidelity = report.Fidelity,
                    Path = path,
                    ExitDirection = report.ExitDirection,
                    StillVisible = report.StillVisible,
                    AppearedInView = report.AppearedInView,
                    DestroyedInView = report.DestroyedInView,
                    CompletedInView = report.CompletedInView,
                    CapturedInView = report.CapturedInView,
                    SideJoinedInView = report.SideJoinedInView,
                    SideLeftInView = report.SideLeftInView,
                    FirstTurnSequence = report.FirstTurnSequence,
                    LastTurnSequence = report.LastTurnSequence,
                    FirstWitnessId = report.FirstWitnessId,
                    FirstWitnessRecordType = report.FirstWitnessRecordType,
                    FirstWitnessOwnerPlayerId = report.FirstWitnessOwnerPlayerId,
                    // A Force report fills the Unit fields, a Structure report the Structure ones and a combat report the Battle ones; the rest stay null, so the one projection serves all three.
                    Subject = new SightingSubject
                    {
                        UnitInstanceId = report.SubjectUnitInstanceId,
                        OwnerPlayerId = report.SubjectOwnerPlayerId,
                        UnitTemplateId = report.SubjectUnitTemplateId,
                        HealthyTroops = report.SubjectHealthyTroops,
                        StructureInstanceId = report.SubjectStructureInstanceId,
                        StructureId = report.SubjectStructureId,
                        PreviousOwnerPlayerId = report.SubjectPreviousOwnerPlayerId,
                        CompletionPercent = report.SubjectCompletionPercent,
                        BattleId = report.SubjectBattleId,
                        GroundHeldByPlayerId = report.SubjectGroundHeldByPlayerId,
                        TroopsLost = report.SubjectTroopsLost
                    }
                }));
            }

            return ServiceResult<IReadOnlyCollection<SightingReport>>.Succeed(disclosedReports);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetPlayerSightingReportsRequest));

            return ServiceResult<IReadOnlyCollection<SightingReport>>.Fail("An unexpected error occurred while retrieving sighting reports.");
        }
    }
}
