using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Intelligence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <inheritdoc cref="IForceSightingFoldService" />
internal class ForceSightingFoldService(
    ILogger<ForceSightingFoldService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IWitnessService witnessService) : IForceSightingFoldService
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The Turn's whole movement feed. Only this Turn's rows are ever needed: a Moved row carries the tile it stepped off, which is what gives a Force its origin step.
            List<MovementHistory> rows = await context.MovementHistory
                .AsNoTracking()
                .Where(row => row.GameId == gameId && row.PlayerTurnId == playerTurnId)
                .ToListAsync(cancelToken);

            if (rows.Count == 0)
            {
                // Nothing moved, appeared or died, so nobody saw anything. There can be no reports from an earlier run to replace either, since those would have needed rows.
                logger.LogDebug("No movement history for Player Turn Id {PlayerTurnId} in Game Id {GameId}, so there are no sightings to raise.", playerTurnId, gameId);
                return ServiceResultBase.Succeed();
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, gameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {gameId} was not found while raising sighting reports.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            IReadOnlyList<ForcePath> paths = ForcePath.BuildAll(rows, geometry);

            // Strength as it stands now, for the Forces still alive. A Force destroyed during the Turn has no row left, and its sightings carry no strength.
            int[] unitInstanceIds = [.. paths.Select(path => path.UnitInstanceId)];

            Dictionary<int, int> healthyTroopsByUnitInstanceId = await context.UnitInstances
                .AsNoTracking()
                .Where(force => unitInstanceIds.Contains(force.Id))
                .Select(force => new { force.Id, force.HealthyCount })
                .ToDictionaryAsync(force => force.Id, force => force.HealthyCount, cancelToken);

            List<PlayerReport> reports = [];
            List<PlayerKnownForce> knownForceSightings = [];

            // One witness query per owner. A Force's owner is never told they were seen by themselves, so each owner's coalition is excluded along with the acting Player's. Today every row in a
            // Turn belongs to the acting Player and this is one query; it becomes several once combat can destroy another Player's Forces during someone else's Turn.
            foreach (IGrouping<int, ForcePath> ownerPaths in paths.GroupBy(path => path.OwnerPlayerId).OrderBy(group => group.Key))
            {
                var query = new WitnessQuery
                {
                    GameId = gameId,
                    PlayerTurnId = playerTurnId,
                    ExcludedPlayerIds = [.. new[] { actingPlayerId, ownerPaths.Key }.Distinct()],
                    Probes =
                    [
                        .. ownerPaths.SelectMany(path => path.Steps.Select(step => new WitnessProbe
                        {
                            Key = step.ProbeKey,
                            Location = step.Location,
                            TurnSequence = step.TurnSequence,
                            Force = new ForceSubject
                            {
                                UnitInstanceId = path.UnitInstanceId,
                                UnitTemplateId = path.UnitTemplateId,
                                HealthyTroops = HealthyTroopsOf(path, healthyTroopsByUnitInstanceId)
                            }
                        }))
                    ]
                };

                ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> witnessResult = await witnessService.FindWitnessesAsync(context, query, cancelToken);

                if (witnessResult.HasSucceeded == false || witnessResult.SuccessValue is null)
                {
                    string error = $"Could not work out who watched Player Id {ownerPaths.Key}'s Forces during Player Turn Id {playerTurnId}.";
                    logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", witnessResult.Errors));
                    return ServiceResultBase.Fail(witnessResult.Errors.Prepend(error));
                }

                foreach (ForcePath path in ownerPaths)
                {
                    IReadOnlyList<CoalitionForceTracks> coalitionTracks = ForceTrackFolder.Fold(path, witnessResult.SuccessValue, geometry);
                    int? healthyTroops = HealthyTroopsOf(path, healthyTroopsByUnitInstanceId);

                    reports.AddRange(SightingRecordBuilder.BuildReports(gameId, playerTurnId, path, coalitionTracks, healthyTroops));
                    knownForceSightings.AddRange(SightingRecordBuilder.BuildKnownForces(gameId, playerTurnId, path, coalitionTracks, healthyTroops));
                }
            }

            await SaveAsync(context, gameId, playerTurnId, reports, knownForceSightings, cancelToken);

            logger.LogInformation(
                "Raised {ReportCount} sighting report(s) and refreshed {KnownForceCount} known Force(s) from {ForceCount} Force(s) in Player Turn Id {PlayerTurnId}, Game Id {GameId}.",
                reports.Count,
                knownForceSightings.Count,
                paths.Count,
                playerTurnId,
                gameId);

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while raising sighting reports for Player Turn Id {playerTurnId} in Game Id {gameId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    private static int? HealthyTroopsOf(ForcePath path, Dictionary<int, int> healthyTroopsByUnitInstanceId)
    {
        return healthyTroopsByUnitInstanceId.TryGetValue(path.UnitInstanceId, out int healthyTroops) ? healthyTroops : null;
    }

    /// <summary>
    /// Replaces the Turn's Force reports and upserts the known Forces, in one transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replacing rather than appending is what makes a re-run safe: the Turn's reports are deleted and raised again from the same history, which gives the same answer.
    /// </para>
    /// <para>
    /// Known Forces are only overwritten by a sighting at least as recent as the one they hold, so a re-run of an older Turn can never roll a Player's memory backwards.
    /// </para>
    /// </remarks>
    private static async Task SaveAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerTurnId,
        List<PlayerReport> reports,
        List<PlayerKnownForce> knownForceSightings,
        CancellationToken cancelToken)
    {
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        // Path tiles first: they hold the foreign key to the reports, which is NoAction.
        await context.PlayerReportPathTiles
            .Where(tile => context.PlayerReports.Any(report => report.Id == tile.PlayerReportId
                && report.RaisedDuringPlayerTurnId == playerTurnId
                && report.ReportType == PlayerReportType.ForceSighted))
            .ExecuteDeleteAsync(cancelToken);

        await context.PlayerReports
            .Where(report => report.RaisedDuringPlayerTurnId == playerTurnId && report.ReportType == PlayerReportType.ForceSighted)
            .ExecuteDeleteAsync(cancelToken);

        // Each report brings its path tiles with it, through the navigation, so they are inserted once the report has its id.
        context.PlayerReports.AddRange(reports);

        if (knownForceSightings.Count > 0)
        {
            int[] knowingPlayerIds = [.. knownForceSightings.Select(sighting => sighting.PlayerId).Distinct()];
            int[] knownUnitInstanceIds = [.. knownForceSightings.Select(sighting => sighting.UnitInstanceId).Distinct()];

            // Tracked, because the ones a sighting supersedes are updated in place. The query can over-fetch a little - every pairing of those Players and Forces - which is harmless.
            Dictionary<(int PlayerId, int UnitInstanceId), PlayerKnownForce> existingByKey = await context.PlayerKnownForces
                .Where(known => known.GameId == gameId && knowingPlayerIds.Contains(known.PlayerId) && knownUnitInstanceIds.Contains(known.UnitInstanceId))
                .ToDictionaryAsync(known => (known.PlayerId, known.UnitInstanceId), cancelToken);

            foreach (PlayerKnownForce sighting in knownForceSightings)
            {
                if (existingByKey.TryGetValue((sighting.PlayerId, sighting.UnitInstanceId), out PlayerKnownForce? existing) == false)
                {
                    context.PlayerKnownForces.Add(sighting);
                    existingByKey[(sighting.PlayerId, sighting.UnitInstanceId)] = sighting;

                    continue;
                }

                if (SightingRecordBuilder.Supersedes(sighting, existing))
                {
                    SightingRecordBuilder.CopySighting(from: sighting, to: existing);
                }
            }
        }

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);
    }
}
