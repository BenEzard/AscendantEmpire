using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Battles.Services;

/// <inheritdoc cref="IBattleLifecycleWriter" />
internal class BattleLifecycleWriter(
    ILogger<BattleLifecycleWriter> logger,
    IPlayerTurnService playerTurnService) : IBattleLifecycleWriter
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> CommenceAsync(AscendantEmpireContext context, Battle battle, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(battle);

        // An id means the Battle has been inserted already; adding it again would fail in the database, which is a worse place to learn it.
        if (battle.Id != 0)
        {
            return Refuse($"Battle Id {battle.Id} already exists, so it cannot be commenced.");
        }

        if (battle.ConcludedAtRoundNumber is not null)
        {
            return Refuse("A Battle cannot be commenced already concluded.");
        }

        if (battle.BattleParticipants.Count == 0)
        {
            return Refuse("A Battle cannot be commenced with no sides in it.");
        }

        int[] duplicateSides = [.. battle.BattleParticipants.GroupBy(participant => participant.PlayerId).Where(side => side.Count() > 1).Select(side => side.Key)];

        if (duplicateSides.Length > 0)
        {
            return Refuse($"A Battle cannot be commenced with Player Id(s) {string.Join(", ", duplicateSides)} on its roster more than once.");
        }

        try
        {
            // One number for the engagement starting, and one for each side already in it. Allocated before anything is added, so a refusal writes nothing.
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, 1 + battle.BattleParticipants.Count, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "commencement", battleId: null, playerTurnId);
            }

            // Two saves have to land together: the Battle, to learn its id, then the rows carrying it. A caller already inside a transaction keeps ownership of it; otherwise this opens one, and
            // disposing it uncommitted rolls both saves back.
            await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancelToken)
                : null;

            context.Battles.Add(battle);

            await context.SaveChangesAsync(cancelToken);

            int sequence = sequenceResult.SuccessValue.First;

            context.BattleHistory.Add(BuildRow(battle, BattleHistoryEventType.Commenced, playerTurnId, sequence));

            // In roster order, so that re-running the same setup writes the same rows in the same order.
            foreach (BattleParticipant participant in battle.BattleParticipants)
            {
                sequence++;

                context.BattleHistory.Add(BuildRow(battle, BattleHistoryEventType.ParticipantJoined, playerTurnId, sequence, participantPlayerId: participant.PlayerId));
            }

            await context.SaveChangesAsync(cancelToken);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancelToken);
            }

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while commencing a Battle on Campaign Map Tile Id {battle.CampaignMapTileId}.";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> JoinAsync(AscendantEmpireContext context, Battle battle, int playerId, int roundNumber, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(battle);

        ServiceResultBase? openRefusal = RefuseUnlessOpen(battle, "joined");
        if (openRefusal is not null)
        {
            return openRefusal;
        }

        if (battle.BattleParticipants.Any(participant => participant.PlayerId == playerId))
        {
            return Refuse($"Player Id {playerId} is already on the roster of Battle Id {battle.Id} and cannot join it again.");
        }

        try
        {
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "join", battle.Id, playerTurnId);
            }

            battle.BattleParticipants.Add(new BattleParticipant
            {
                BattleId = battle.Id,
                PlayerId = playerId,
                JoinedAtRoundNumber = roundNumber,
                IsStillInBattle = true,
                ExitReason = BattleExitReason.NotApplicable
            });

            context.BattleHistory.Add(BuildRow(battle, BattleHistoryEventType.ParticipantJoined, playerTurnId, sequenceResult.SuccessValue.First, participantPlayerId: playerId));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while Player Id {playerId} joined Battle Id {battle.Id}.";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> LeaveAsync(
        AscendantEmpireContext context,
        Battle battle,
        int playerId,
        BattleExitReason exitReason,
        int troopsLost,
        int roundNumber,
        int playerTurnId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(battle);

        ServiceResultBase? openRefusal = RefuseUnlessOpen(battle, "left");
        if (openRefusal is not null)
        {
            return openRefusal;
        }

        ServiceResultBase? outcomeRefusal = RefuseUnlessOutcomeIsUsable(battle, exitReason, troopsLost);
        if (outcomeRefusal is not null)
        {
            return outcomeRefusal;
        }

        BattleParticipant? participant = battle.BattleParticipants.SingleOrDefault(side => side.PlayerId == playerId);

        if (participant is null)
        {
            return Refuse($"Player Id {playerId} is not on the roster of Battle Id {battle.Id} and cannot leave it.");
        }

        if (participant.IsStillInBattle == false)
        {
            return Refuse($"Player Id {playerId} has already left Battle Id {battle.Id} and cannot leave it again.");
        }

        try
        {
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "departure", battle.Id, playerTurnId);
            }

            StampDeparture(participant, exitReason, roundNumber);

            context.BattleHistory.Add(BuildRow(
                battle,
                BattleHistoryEventType.ParticipantLeft,
                playerTurnId,
                sequenceResult.SuccessValue.First,
                participantPlayerId: playerId,
                exitReason: exitReason,
                troopsLost: troopsLost));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while Player Id {playerId} left Battle Id {battle.Id}.";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> ConcludeAsync(AscendantEmpireContext context, Battle battle, BattleConclusion conclusion, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(conclusion);

        ServiceResultBase? openRefusal = RefuseUnlessOpen(battle, "concluded");
        if (openRefusal is not null)
        {
            return openRefusal;
        }

        if (conclusion.VictorPlayerId is int victorPlayerId && battle.BattleParticipants.Any(side => side.PlayerId == victorPlayerId) == false)
        {
            return Refuse($"Player Id {victorPlayerId} is not on the roster of Battle Id {battle.Id} and cannot have won it.");
        }

        BattleParticipant[] stillInBattle = [.. battle.BattleParticipants.Where(side => side.IsStillInBattle)];

        // Every side still in it is written out by this call, and no side that already left may be written out twice. Compared as sets, since the order the caller supplies them in is theirs to
        // choose; a mismatch either way means the caller's picture of the roster has drifted from the Battle's, which is not something to paper over.
        ServiceResultBase? coverageRefusal = RefuseUnlessOutcomesCoverRoster(battle, conclusion, stillInBattle);
        if (coverageRefusal is not null)
        {
            return coverageRefusal;
        }

        foreach (BattleSideOutcome outcome in conclusion.Outcomes)
        {
            ServiceResultBase? outcomeRefusal = RefuseUnlessOutcomeIsUsable(battle, outcome.ExitReason, outcome.TroopsLost);
            if (outcomeRefusal is not null)
            {
                return outcomeRefusal;
            }
        }

        try
        {
            // One number per side written out, and one for the conclusion itself.
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, conclusion.Outcomes.Count + 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "conclusion", battle.Id, playerTurnId);
            }

            int sequence = sequenceResult.SuccessValue.First;

            foreach (BattleSideOutcome outcome in conclusion.Outcomes)
            {
                BattleParticipant participant = stillInBattle.Single(side => side.PlayerId == outcome.PlayerId);

                StampDeparture(participant, outcome.ExitReason, conclusion.RoundNumber);

                context.BattleHistory.Add(BuildRow(
                    battle,
                    BattleHistoryEventType.ParticipantLeft,
                    playerTurnId,
                    sequence,
                    participantPlayerId: outcome.PlayerId,
                    exitReason: outcome.ExitReason,
                    troopsLost: outcome.TroopsLost));

                sequence++;
            }

            battle.ConcludedAtRoundNumber = conclusion.RoundNumber;
            battle.VictorId = conclusion.VictorPlayerId;
            battle.UpdatedAt = DateTimeOffset.UtcNow;

            context.BattleHistory.Add(BuildRow(
                battle,
                BattleHistoryEventType.Concluded,
                playerTurnId,
                sequence,
                groundHeldByPlayerId: conclusion.GroundHeldByPlayerId));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while concluding Battle Id {battle.Id}.";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// Refuses a Battle that has already concluded, since nothing more can happen in it.
    /// </summary>
    /// <param name="pastParticiple">How the refused action reads in the message - "joined", "left", "concluded".</param>
    /// <returns><see langword="null"/> when the Battle may be changed; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessOpen(Battle battle, string pastParticiple)
    {
        if (battle.ConcludedAtRoundNumber is int concludedAtRoundNumber)
        {
            return Refuse($"Battle Id {battle.Id} concluded in Round {concludedAtRoundNumber} and cannot be {pastParticiple}.");
        }

        return null;
    }

    /// <summary>
    /// Refuses an exit reason or a loss figure that cannot be recorded against a departure.
    /// </summary>
    /// <remarks>
    /// <see cref="BattleExitReason.NotApplicable"/> is what a side still in the fight carries, so a departure row claiming it would be a row saying the side both left and did not.
    /// </remarks>
    /// <returns><see langword="null"/> when the departure may be recorded; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessOutcomeIsUsable(Battle battle, BattleExitReason exitReason, int troopsLost)
    {
        if (exitReason == BattleExitReason.NotApplicable)
        {
            return Refuse($"A side cannot leave Battle Id {battle.Id} for no reason; {nameof(BattleExitReason.NotApplicable)} is what a side still in it carries.");
        }

        if (Enum.IsDefined(exitReason) == false)
        {
            return Refuse($"A side cannot leave Battle Id {battle.Id} for reason {(int)exitReason}, which is not a {nameof(BattleExitReason)}.");
        }

        if (troopsLost < 0)
        {
            return Refuse($"A side cannot leave Battle Id {battle.Id} having lost {troopsLost} troops.");
        }

        return null;
    }

    /// <summary>
    /// Refuses a conclusion whose outcomes are not exactly the sides still in the Battle.
    /// </summary>
    /// <returns><see langword="null"/> when the conclusion may be recorded; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessOutcomesCoverRoster(Battle battle, BattleConclusion conclusion, IReadOnlyCollection<BattleParticipant> stillInBattle)
    {
        int[] outcomePlayerIds = [.. conclusion.Outcomes.Select(outcome => outcome.PlayerId)];

        if (outcomePlayerIds.Length != outcomePlayerIds.Distinct().Count())
        {
            return Refuse($"Battle Id {battle.Id} cannot be concluded with the same side given an outcome more than once.");
        }

        int[] stillInPlayerIds = [.. stillInBattle.Select(side => side.PlayerId)];
        int[] missing = [.. stillInPlayerIds.Except(outcomePlayerIds).Order()];
        int[] unexpected = [.. outcomePlayerIds.Except(stillInPlayerIds).Order()];

        if (missing.Length > 0 || unexpected.Length > 0)
        {
            return Refuse(
                $"Battle Id {battle.Id} cannot be concluded: its outcomes must cover exactly the sides still in it. " +
                $"Missing Player Id(s): {FormatPlayerIds(missing)}. Player Id(s) not still in it: {FormatPlayerIds(unexpected)}.");
        }

        return null;
    }

    /// <summary>
    /// Stamps a participant as out of the Battle.
    /// </summary>
    private static void StampDeparture(BattleParticipant participant, BattleExitReason exitReason, int roundNumber)
    {
        participant.IsStillInBattle = false;
        participant.LeftAtRoundNumber = roundNumber;
        participant.ExitReason = exitReason;
    }

    /// <summary>
    /// Renders a set of Player ids for a refusal message, so an empty half of a roster mismatch reads as "none" rather than as nothing at all.
    /// </summary>
    private static string FormatPlayerIds(IReadOnlyCollection<int> playerIds)
    {
        return playerIds.Count == 0 ? "none" : string.Join(", ", playerIds);
    }

    /// <summary>
    /// Logs and returns a refusal of a change the writer will not make.
    /// </summary>
    private ServiceResultBase Refuse(string error)
    {
        logger.LogWarning("{Error}", error);

        return ServiceResultBase.Fail(error);
    }

    /// <summary>
    /// Turns a failed sequence allocation into the writer's own refusal, keeping the allocator's reasons.
    /// </summary>
    /// <remarks>
    /// Logged as a warning rather than an error: the allocator has already logged its own failure, and the usual cause - a Turn that has just been closed - is a race rather than a fault.
    /// </remarks>
    private ServiceResultBase RefuseForMissingSequence(ServiceResult<TurnSequenceBlock> sequenceResult, string action, int? battleId, int playerTurnId)
    {
        string error = $"No sequence number could be allocated from Player Turn Id {playerTurnId}, so the {action} was not recorded and has not been made.";

        logger.LogWarning("{Error} Battle Id: {BattleId}. Allocator errors: {Errors}", error, battleId, string.Join("; ", sequenceResult.Errors));

        return ServiceResultBase.Fail(sequenceResult.Errors.Prepend(error));
    }

    /// <summary>
    /// Builds one history row for a Battle, copying its Game and tile so the row says where the fighting was without a join.
    /// </summary>
    private static BattleHistory BuildRow(
        Battle battle,
        BattleHistoryEventType eventType,
        int playerTurnId,
        int turnSequence,
        int? participantPlayerId = null,
        BattleExitReason? exitReason = null,
        int? troopsLost = null,
        int? groundHeldByPlayerId = null)
    {
        return new BattleHistory
        {
            GameId = battle.GameId,
            BattleId = battle.Id,
            EventType = eventType,
            ParticipantPlayerId = participantPlayerId,
            ExitReason = exitReason,
            TroopsLost = troopsLost,
            GroundHeldByPlayerId = groundHeldByPlayerId,
            CampaignMapTileId = battle.CampaignMapTileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = turnSequence
        };
    }
}
