
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;
using AscendantEmpire.BackendContracts.Turns.GetCampaignRoundInfo;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;
public record CreateBattleTurnQueueRequest(
    int GameId,
    int CampaignMapTileId,
    IReadOnlyCollection<int> PlayerIdsInTurnOrder
) : IRequest<ServiceResult<FirstPlayerInRound>>;

internal class CreateBattleTurnQueueRequestValidator : Validator<CreateBattleTurnQueueRequest>
{
    public CreateBattleTurnQueueRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0)
            .WithMessage("Game Id is required.");

        RuleFor(x => x.CampaignMapTileId)
            .GreaterThan(0)
            .WithMessage("Campaign Map Tile Id is required.");

        RuleFor(x => x.PlayerIdsInTurnOrder)
            .NotEmpty()
            .WithMessage("At least one Player is required to create a Battle Turn Queue.");

        RuleFor(x => x.PlayerIdsInTurnOrder)
            .Must(x => x.Distinct().Count() == x.Count)
            .When(x => x.PlayerIdsInTurnOrder is not null)
            .WithMessage("Duplicate Player Ids are not allowed.");
    }
}

internal class CreateBattleTurnQueueHandler(
    ILogger<CreateBattleTurnQueueHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMediator mediator
) : IRequestHandler<CreateBattleTurnQueueRequest, ServiceResult<FirstPlayerInRound>>
{
    public async Task<ServiceResult<FirstPlayerInRound>> Handle(
        CreateBattleTurnQueueRequest request,
        CancellationToken cancelToken)
    {
        try
        {
            // Do initial request validation
            var validator = new CreateBattleTurnQueueRequestValidator();
            var validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(CreateBattleTurnQueueRequest)}.";

                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<FirstPlayerInRound>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            await using var transaction = await context.Database.BeginTransactionAsync(cancelToken);

            bool gameExists = await context.GameIndex
                .Where(gi => gi.Id == request.GameId)
                .Where(gi => gi.PlayStatus == GamePlayStatus.InProgress || gi.PlayStatus == GamePlayStatus.SetupInProgress)
                .AnyAsync(cancelToken);

            if (gameExists == false)
            {
                const string error = "Game was not found or is not in a valid state for creating a Battle Turn Queue.";

                logger.LogWarning("{Error} Game Id: {GameId}", error, request.GameId);

                return ServiceResult<FirstPlayerInRound>.NotFound(error);
            }

            // Check if all the requested Players are active in the Game
            ServiceResult<IReadOnlyCollection<BackendContracts.Players.GetPlayers.Player>> playersResult =
                await mediator.Send(new GetPlayersRequest(request.GameId, PlayerStatuses: [PlayerStatus.Active], PlayerIds: request.PlayerIdsInTurnOrder), cancelToken);

            if (playersResult.HasSucceeded == false || playersResult.SuccessValue is null || playersResult.SuccessValue.Count == 0)
            {
                logger.LogWarning("An attempt to get the Players was not successful (Error: {Err})", string.Join(Environment.NewLine, playersResult.Errors));
                return ServiceResult<FirstPlayerInRound>.Fail(playersResult.Errors.Prepend("Unable to retrieve Player information for the specified Player Ids."));
            }

            List<DTFaction> activeInvolvedPlayers = [.. playersResult.SuccessValue
                .Select(p => new DTFaction
                {
                    Id = p.Id,
                    FactionName = p.FactionName
                })];

            List<int> invalidPlayerIds = [.. request.PlayerIdsInTurnOrder.Except(activeInvolvedPlayers.Select(p => p.Id))];
            if (invalidPlayerIds.Count > 0)
            {
                string error = $"One or more Players are not active in this Game. Player Ids: {string.Join(", ", invalidPlayerIds)}.";

                logger.LogWarning("{Error} Game Id: {GameId}", error, request.GameId);

                return ServiceResult<FirstPlayerInRound>.Fail(error);
            }

            bool activeBattleAlreadyExistsForTile = await context.Battles
                .Where(b => b.GameId == request.GameId)
                .Where(b => b.CampaignMapTileId == request.CampaignMapTileId)
                .Where(b => b.ConcludedAtRoundNumber == null)
                .AnyAsync(cancelToken);

            if (activeBattleAlreadyExistsForTile)
            {
                const string error = "An active Battle already exists for this Campaign Map Tile.";

                logger.LogWarning(
                    "{Error} Game Id: {GameId}, Campaign Map Tile Id: {CampaignMapTileId}",
                    error,
                    request.GameId,
                    request.CampaignMapTileId);

                return ServiceResult<FirstPlayerInRound>.Fail(error);
            }

            ServiceResult<CampaignRound> roundResult = await GetCampaignRoundAsync(request.GameId);
            if (roundResult.HasSucceeded == false)
            {
                logger.LogWarning("An attempt to get the Campaign Round was not successful (Error: {Err})", string.Join(Environment.NewLine, roundResult.Errors));
                return ServiceResult<FirstPlayerInRound>.Fail(roundResult.Errors.Prepend("Unable to retrieve current Campaign Round information."));
            }
            int commencedAtRoundNumber = roundResult.SuccessValue!.CurrentRoundNumber;
            
            ServiceResult<string> globalBattleNameResult = await GenerateDefaultBattleNameAsync(request.GameId, request.CampaignMapTileId, activeInvolvedPlayers, commencedAtRoundNumber);

            if (globalBattleNameResult.HasSucceeded == false)
            {
                logger.LogWarning("An attempt to generate a default Battle name was not successful (Error: {Err})", string.Join(Environment.NewLine, globalBattleNameResult.Errors));
                return ServiceResult<FirstPlayerInRound>.Fail(globalBattleNameResult.Errors.Prepend("Unable to generate a default Battle name."));
            }

            string globalBattleName = globalBattleNameResult.SuccessValue!;

            DateTimeOffset now = DateTimeOffset.UtcNow;

            // TODO: route this through IBattleLifecycleWriter.CommenceAsync. Building the Battle and its roster here bypasses the single writer, so the engagement starts with no BattleHistory
            // rows and nobody watching the tile is ever told the fighting broke out. Harmless today - this handler has no production caller and no route - but it must not gain one as it stands.
            // See docs/design/intelligence-and-sightings.md.
            var battle = new Battle
            {
                GameId = request.GameId,
                CampaignMapTileId = request.CampaignMapTileId,
                GlobalBattleName = globalBattleName, 
                CommencedAtRoundNumber = commencedAtRoundNumber,
                OriginalTileStatus = Domain.Enums.Maps.TileOwnershipStatus.Unclaimed, // TODO: To be loaded from the current state of the Campaign Map
                OriginalTileOwnerId = null, // TODO: To be loaded from the current state of the Campaign Map
                ConcludedAtRoundNumber = null,
                VictorId = null,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = [],
                BattleParticipants = [.. request.PlayerIdsInTurnOrder
                    .Select(playerId => new BattleParticipant
                    {
                        PlayerId = playerId,
                        JoinedAtRoundNumber = commencedAtRoundNumber,
                        IsStillInBattle = true,
                        LeftAtRoundNumber = null,
                        ExitReason = BattleExitReason.NotApplicable,

                        PlayerBattleName = null,
                        RowVersion = []
                    })]
            };

            var turnQueue = new TurnQueue
            {
                GameId = request.GameId,
                QueueType = TurnQueueType.Battle,
                Battle = battle,
                CurrentRoundNumber = commencedAtRoundNumber,
                CurrentTurnStartAt = null,
                CurrentScheduledTurnEndAt = null,
                CurrentActualTurnEndAt = null,
                IsCompleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = [],
                TurnQueueParticipants = [.. request.PlayerIdsInTurnOrder
                    .Select((playerId, index) => new TurnQueueParticipant
                    {
                        PlayerId = playerId,
                        TurnOrder = index + 1,
                        Status = TurnQueueParticipantStatus.Pending,
                        IsActiveInLane = true,
                        LastCompletedRoundNumber = null,
                        LastTurnStartedAt = null,
                        LastTurnEndedAt = null,
                        RowVersion = []
                    })]
            };

            context.Battles.Add(battle);
            context.TurnQueues.Add(turnQueue);

            await context.SaveChangesAsync(cancelToken);
            await transaction.CommitAsync(cancelToken);

            TurnQueueParticipant firstParticipant = turnQueue.TurnQueueParticipants
                .OrderBy(p => p.TurnOrder)
                .First();

            return ServiceResult<FirstPlayerInRound>.Succeed(new FirstPlayerInRound
            {
                TurnQueueId = turnQueue.Id,
                PlayerId = firstParticipant.PlayerId,
                RoundNumber = turnQueue.CurrentRoundNumber
            });
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception creating Battle Turn Queue.";

            // Walk the full inner exception chain so FK/SQL constraint details are not swallowed.
            IEnumerable<string> messages = UnwrapMessages(ex);

            logger.LogError(error + " {Error}", string.Join(Environment.NewLine, messages));

            return ServiceResult<FirstPlayerInRound>.Fail(error);
        }
    }

    private static IEnumerable<string> UnwrapMessages(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            yield return current.Message;
        }
    }

    private async Task<ServiceResult<CampaignRound>> GetCampaignRoundAsync(int gameId)
    {
        return await mediator.Send(new GetCampaignRoundRequest(gameId), CancellationToken.None);
    }

    private struct DTFaction
    {
        public int Id;
        public string FactionName;
    }

    /// <summary>
    /// Generate a default Battle name to be used for the Battle based on the Campaign Map Tile and the Players involved.
    /// </summary>
    private async Task<ServiceResult<string>> GenerateDefaultBattleNameAsync(int gameId, int campaignMapTileId, IEnumerable<DTFaction> factions, int roundNumber)
    {
        // TODO: Complete this logic
        return ServiceResult<string>.Succeed($"Battle {roundNumber} between {string.Join(" and ", factions.Select(f => f.FactionName))}");
    }
}