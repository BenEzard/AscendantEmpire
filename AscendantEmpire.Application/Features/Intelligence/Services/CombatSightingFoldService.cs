using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Intelligence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <inheritdoc cref="ICombatSightingFoldService" />
internal class CombatSightingFoldService(
    ILogger<CombatSightingFoldService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ISightDataReader sightDataReader,
    IWitnessService witnessService) : ICombatSightingFoldService
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The Turn's whole combat feed.
            List<BattleHistory> rows = await context.BattleHistory
                .AsNoTracking()
                .Where(row => row.GameId == gameId && row.PlayerTurnId == playerTurnId)
                .ToListAsync(cancelToken);

            if (rows.Count == 0)
            {
                // No fighting started, changed or ended, so nobody saw anything. There can be no reports from an earlier run to replace either, since those would have needed rows.
                logger.LogDebug("No battle history for Player Turn Id {PlayerTurnId} in Game Id {GameId}, so there are no combat sightings to raise.", playerTurnId, gameId);
                return ServiceResultBase.Succeed();
            }

            CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(context, gameId, cancelToken);

            if (geometry is null)
            {
                string error = $"Game Id {gameId} was not found while raising combat sighting reports.";
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
                string error = $"Player Turn Id {playerTurnId} was not found while raising combat sighting reports.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            IReadOnlyList<BattleTimeline> timelines = BattleTimeline.BuildAll(rows, geometry);

            IReadOnlyDictionary<int, int[]> rostersByBattleId = await ReadRostersAsync(context, timelines, cancelToken);

            List<PlayerReport> reports = [];

            // One query per engagement, not one for the Turn: the sides to exclude differ from Battle to Battle, and a single query excluding all of them would silence a combatant in one Battle
            // from witnessing another halfway across the map. See the interface's remarks.
            foreach (BattleTimeline timeline in timelines)
            {
                ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> witnessResult =
                    await witnessService.FindWitnessesAsync(context, BuildQuery(gameId, playerTurnId, actingPlayerId, timeline, rostersByBattleId, lastSequence), cancelToken);

                if (witnessResult.HasSucceeded == false || witnessResult.SuccessValue is null)
                {
                    string error = $"Could not work out who watched Battle Id {timeline.BattleId} during Player Turn Id {playerTurnId}.";
                    logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", witnessResult.Errors));
                    return ServiceResultBase.Fail(witnessResult.Errors.Prepend(error));
                }

                IReadOnlyList<CoalitionEngagementSighting> sightings = EngagementEventFolder.Fold(timeline, witnessResult.SuccessValue);

                reports.AddRange(CombatSightingRecordBuilder.BuildReports(gameId, playerTurnId, timeline, sightings));
            }

            await SaveAsync(context, playerTurnId, reports, cancelToken);

            logger.LogInformation(
                "Raised {ReportCount} combat report(s) from {BattleCount} engagement(s) in Player Turn Id {PlayerTurnId}, Game Id {GameId}.",
                reports.Count,
                timelines.Count,
                playerTurnId,
                gameId);

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while raising combat sighting reports for Player Turn Id {playerTurnId} in Game Id {gameId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// Reads the full roster of every engagement in the Turn's feed.
    /// </summary>
    /// <remarks>
    /// Taken from <c>BattleParticipant</c> rather than from the Turn's rows, because a side that joined in an earlier Round has no <c>ParticipantJoined</c> row this Turn and would otherwise be
    /// left un-excluded - told about a battle it is standing in.
    /// </remarks>
    private static async Task<IReadOnlyDictionary<int, int[]>> ReadRostersAsync(AscendantEmpireContext context, IReadOnlyList<BattleTimeline> timelines, CancellationToken cancelToken)
    {
        int[] battleIds = [.. timelines.Select(timeline => timeline.BattleId)];

        List<(int BattleId, int PlayerId)> roster = await context.BattleParticipants
            .AsNoTracking()
            .Where(participant => battleIds.Contains(participant.BattleId))
            .Select(participant => new ValueTuple<int, int>(participant.BattleId, participant.PlayerId))
            .ToListAsync(cancelToken);

        return roster
            .GroupBy(participant => participant.BattleId)
            .ToDictionary(battleRoster => battleRoster.Key, battleRoster => battleRoster.Select(participant => participant.PlayerId).ToArray());
    }

    /// <summary>
    /// Builds the witness query for one engagement: every event at its own sequence, plus one more as at the end of the Turn when the fighting is still going on.
    /// </summary>
    /// <remarks>
    /// The probes carry no Force subject, so each answer is the tile's own fidelity. That is right for an engagement: what a watcher makes out is the ground being fought over, not any one Force
    /// standing on it.
    /// </remarks>
    private static WitnessQuery BuildQuery(
        int gameId,
        int playerTurnId,
        int actingPlayerId,
        BattleTimeline timeline,
        IReadOnlyDictionary<int, int[]> rostersByBattleId,
        int lastSequence)
    {
        int[] roster = rostersByBattleId.TryGetValue(timeline.BattleId, out int[]? battleRoster) ? battleRoster : [];

        List<WitnessProbe> probes =
        [
            .. timeline.Events.Select(engagementEvent => new WitnessProbe
            {
                Key = engagementEvent.ProbeKey,
                Location = timeline.Location,
                TurnSequence = engagementEvent.TurnSequence
            })
        ];

        if (timeline.HasConcluded == false)
        {
            probes.Add(new WitnessProbe
            {
                Key = timeline.EndOfTurnProbeKey,
                Location = timeline.Location,
                TurnSequence = lastSequence + 1
            });
        }

        return new WitnessQuery
        {
            GameId = gameId,
            PlayerTurnId = playerTurnId,
            ExcludedPlayerIds = [.. roster.Append(actingPlayerId).Distinct()],
            Probes = probes
        };
    }

    /// <summary>
    /// Replaces the Turn's combat reports, in one transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replacing rather than appending is what makes a re-run safe. Only <see cref="PlayerReportType.CombatSighted"/> reports are deleted, so the Force and Structure steps' reports for the same
    /// Turn are never touched, and a combat report has no path tiles to delete first.
    /// </para>
    /// <para>
    /// The simplest of the three saves: there is no known-object row to upsert, so no recency rule applies here.
    /// </para>
    /// </remarks>
    private static async Task SaveAsync(AscendantEmpireContext context, int playerTurnId, List<PlayerReport> reports, CancellationToken cancelToken)
    {
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        await context.PlayerReports
            .Where(report => report.RaisedDuringPlayerTurnId == playerTurnId && report.ReportType == PlayerReportType.CombatSighted)
            .ExecuteDeleteAsync(cancelToken);

        context.PlayerReports.AddRange(reports);

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);
    }
}
