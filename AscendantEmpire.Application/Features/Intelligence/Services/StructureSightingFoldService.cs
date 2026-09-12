using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Intelligence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <inheritdoc cref="IStructureSightingFoldService" />
internal class StructureSightingFoldService(
    ILogger<StructureSightingFoldService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IWitnessService witnessService) : IStructureSightingFoldService
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The Turn's whole structure feed.
            List<StructureHistory> rows = await context.StructureHistory
                .AsNoTracking()
                .Where(row => row.GameId == gameId && row.PlayerTurnId == playerTurnId)
                .ToListAsync(cancelToken);

            if (rows.Count == 0)
            {
                // Nothing was founded, built, taken or razed, so nobody saw anything. There can be no reports from an earlier run to replace either, since those would have needed rows.
                logger.LogDebug("No structure history for Player Turn Id {PlayerTurnId} in Game Id {GameId}, so there are no structure sightings to raise.", playerTurnId, gameId);
                return ServiceResultBase.Succeed();
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, gameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {gameId} was not found while raising structure sighting reports.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            // The end of the Turn, as a sequence: one past the last number anything in the Turn was given. An Observer lost at any point during the Turn has gone by then.
            int? lastAllocatedSequence = await context.PlayerTurns
                .AsNoTracking()
                .Where(turn => turn.Id == playerTurnId)
                .Select(turn => (int?)turn.LastAllocatedSequence)
                .SingleOrDefaultAsync(cancelToken);

            if (lastAllocatedSequence is not int lastSequence)
            {
                string error = $"Player Turn Id {playerTurnId} was not found while raising structure sighting reports.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            IReadOnlyList<StructureTimeline> timelines = StructureTimeline.BuildAll(rows, geometry);

            // One query for every Structure. Unlike the Force step it is not split by owner: only the acting Player is excluded, whoever owns the Structure. See the interface's remarks.
            var query = new WitnessQuery
            {
                GameId = gameId,
                PlayerTurnId = playerTurnId,
                ExcludedPlayerIds = [actingPlayerId],
                Probes =
                [
                    // Every event, at its own sequence. No Force subject, so the answer is the tile's fidelity.
                    .. timelines.SelectMany(timeline => timeline.Events.Select(structureEvent => new WitnessProbe
                    {
                        Key = structureEvent.ProbeKey,
                        Location = timeline.Location,
                        TurnSequence = structureEvent.TurnSequence
                    })),
                    // And every Structure still standing, as the Turn ended, to learn who can still see it.
                    .. timelines.Where(timeline => timeline.IsRazed == false).Select(timeline => new WitnessProbe
                    {
                        Key = timeline.EndOfTurnProbeKey,
                        Location = timeline.Location,
                        TurnSequence = lastSequence + 1
                    })
                ]
            };

            ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> witnessResult = await witnessService.FindWitnessesAsync(context, query, cancelToken);

            if (witnessResult.HasSucceeded == false || witnessResult.SuccessValue is null)
            {
                string error = $"Could not work out who watched the Structures changed during Player Turn Id {playerTurnId}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", witnessResult.Errors));
                return ServiceResultBase.Fail(witnessResult.Errors.Prepend(error));
            }

            List<PlayerReport> reports = [];
            List<PlayerKnownStructure> knownStructureSightings = [];

            foreach (StructureTimeline timeline in timelines)
            {
                IReadOnlyList<CoalitionStructureSighting> sightings = StructureEventFolder.Fold(timeline, witnessResult.SuccessValue);

                reports.AddRange(StructureSightingRecordBuilder.BuildReports(gameId, playerTurnId, timeline, sightings));
                knownStructureSightings.AddRange(StructureSightingRecordBuilder.BuildKnownStructures(gameId, playerTurnId, timeline, sightings));
            }

            await SaveAsync(context, gameId, playerTurnId, reports, knownStructureSightings, cancelToken);

            logger.LogInformation(
                "Raised {ReportCount} structure report(s) and refreshed {KnownStructureCount} known Structure(s) from {StructureCount} Structure(s) in Player Turn Id {PlayerTurnId}, Game Id {GameId}.",
                reports.Count,
                knownStructureSightings.Count,
                timelines.Count,
                playerTurnId,
                gameId);

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while raising structure sighting reports for Player Turn Id {playerTurnId} in Game Id {gameId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// Replaces the Turn's Structure reports and upserts the known Structures, in one transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replacing rather than appending is what makes a re-run safe. Only <see cref="PlayerReportType.StructureSighted"/> reports are deleted, so the Force step's reports for the same Turn are never
    /// touched, and a Structure report has no path tiles to delete first.
    /// </para>
    /// <para>
    /// Known Structures are only overwritten by a sighting at least as recent as the one they hold, so a re-run of an older Turn can never roll a Player's memory backwards.
    /// </para>
    /// </remarks>
    private static async Task SaveAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerTurnId,
        List<PlayerReport> reports,
        List<PlayerKnownStructure> knownStructureSightings,
        CancellationToken cancelToken)
    {
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        await context.PlayerReports
            .Where(report => report.RaisedDuringPlayerTurnId == playerTurnId && report.ReportType == PlayerReportType.StructureSighted)
            .ExecuteDeleteAsync(cancelToken);

        context.PlayerReports.AddRange(reports);

        if (knownStructureSightings.Count > 0)
        {
            int[] knowingPlayerIds = [.. knownStructureSightings.Select(sighting => sighting.PlayerId).Distinct()];
            int[] knownStructureInstanceIds = [.. knownStructureSightings.Select(sighting => sighting.StructureInstanceId).Distinct()];

            // Tracked, because the ones a sighting supersedes are updated in place. The query can over-fetch a little - every pairing of those Players and Structures - which is harmless.
            Dictionary<(int PlayerId, int StructureInstanceId), PlayerKnownStructure> existingByKey = await context.PlayerKnownStructures
                .Where(known => known.GameId == gameId && knowingPlayerIds.Contains(known.PlayerId) && knownStructureInstanceIds.Contains(known.StructureInstanceId))
                .ToDictionaryAsync(known => (known.PlayerId, known.StructureInstanceId), cancelToken);

            foreach (PlayerKnownStructure sighting in knownStructureSightings)
            {
                if (existingByKey.TryGetValue((sighting.PlayerId, sighting.StructureInstanceId), out PlayerKnownStructure? existing) == false)
                {
                    context.PlayerKnownStructures.Add(sighting);
                    existingByKey[(sighting.PlayerId, sighting.StructureInstanceId)] = sighting;

                    continue;
                }

                if (StructureSightingRecordBuilder.Supersedes(sighting, existing))
                {
                    StructureSightingRecordBuilder.CopySighting(from: sighting, to: existing);
                }
            }
        }

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);
    }
}
