using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.AddCampaignTurnsToRoster;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Turns;

public record AddCampaignTurnsToRosterRequest(
    Guid GameGuid
) : IRequest<ServiceResult<FirstPlayerInRound>>;

internal class AddCampaignTurnsToRosterRequestValidator : AbstractValidator<AddCampaignTurnsToRosterRequest>
{
    public AddCampaignTurnsToRosterRequestValidator()
    {
        RuleFor(x => x.GameGuid)
            .NotEmpty().WithMessage("GameGuid is required.");
        RuleFor(r => r.GameGuid)
         .Must(guid => guid != Guid.Empty)
         .WithMessage("GameGuid must be a valid non-empty GUID.");
    }
}

internal class AddCampaignTurnsToRosterHandler(
    ILogger<AddCampaignTurnsToRosterHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<AddCampaignTurnsToRosterRequest, ServiceResult<FirstPlayerInRound>>
{
    public async Task<ServiceResult<FirstPlayerInRound>> Handle(AddCampaignTurnsToRosterRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new AddCampaignTurnsToRosterRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(AddCampaignTurnsToRosterRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<FirstPlayerInRound>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Load the game info
            List<DTActivePlayer> activePlayers = await (
                 from gi in context.GameIndex.AsNoTracking()
                 where (gi.PlayStatus == GamePlayStatus.InProgress || gi.PlayStatus == GamePlayStatus.SetupInProgress)
                 join p in context.Players.AsNoTracking()
                     on gi.Id equals p.GameId
                 where gi.GameGuid == request.GameGuid
                 where p.Status == PlayerStatus.Active
                 select new DTActivePlayer
                 {
                     GameId = gi.Id,
                     PlayerId = p.Id,
                     PlayerGuid = p.Guid
                 }).ToListAsync(cancelToken);

            if (activePlayers.Count == 0)
            {
                const string error = "No active Players or Game found while trying to Add Campaign Turns to Roster.";
                logger.LogWarning(error + "Game Guid: {guid}", request.GameGuid);
                return ServiceResult<FirstPlayerInRound>.NotFound(error);
            }

            int gameId = activePlayers[0].GameId;

            // Check to see if there are any Roster records for this game
            bool anyRecords = await context.TurnRosters
                .AsNoTracking()
                .AnyAsync(r => r.GameId == gameId, cancelToken);

            // If no records, this is the first round, so randomise the Player order
            IEnumerable<TurnRoster> newRounds;
            if (anyRecords == false)
            {
                newRounds = activePlayers
                    .OrderBy(_ => Guid.NewGuid())
                    .Select((r, index) => new TurnRoster()
                    {
                        GameId = r.GameId,
                        PlayerId = r.PlayerId,
                        TurnOrder = index + 1,
                        BattleId = (int?)null,
                        PlayerStillInBattle = null,
                        PlayerBattleName = null,
                        Status = TurnStatus.Pending,
                        TurnStartTime = null,
                        TurnEndTime = null,
                        ScheduledTurnEndTime = null,
                        RoundNumber = 1,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
            }
            else
            {
                // If there are records, get the last Round for the Game, and duplicate the Roster for the next Round
                int lastRoundNumber = await context.TurnRosters
                    .AsNoTracking()
                    .Where(r => r.GameId == gameId)
                    .MaxAsync(r => r.RoundNumber, cancelToken);

                newRounds = await context.TurnRosters
                    .AsNoTracking()
                    .Where(r => r.GameId == gameId && r.BattleId == null && r.RoundNumber == lastRoundNumber)
                    .OrderBy(r => r.TurnOrder)
                    .Select(r => new TurnRoster
                    {
                        GameId = r.GameId,
                        PlayerId = r.PlayerId,
                        TurnOrder = r.TurnOrder,          // keep the same roster order
                        BattleId = null,
                        PlayerStillInBattle = null,
                        PlayerBattleName = null,
                        Status = TurnStatus.Pending,
                        TurnStartTime = null,
                        TurnEndTime = null,
                        ScheduledTurnEndTime = null,
                        RoundNumber = lastRoundNumber + 1,
                        CreatedAt = DateTimeOffset.UtcNow
                    })
                    .ToListAsync(cancelToken);
            }

            context.TurnRosters.AddRange(newRounds);

            await context.SaveChangesAsync(cancelToken);

            TurnRoster? firstPlayerInRound = newRounds
                .OrderBy(r => r.TurnOrder)
                .FirstOrDefault();

            if (firstPlayerInRound == null)
            {
                const string error = "Failed to determine first Player in Round after adding Campaign Turns to Roster.";
                logger.LogError(error + " Game Id: {gameId}", gameId);
                return ServiceResult<FirstPlayerInRound>.InternalServerError(error);
            }

            return ServiceResult<FirstPlayerInRound>.Succeed(new FirstPlayerInRound
            {
                PlayerGuid = activePlayers.Single(p => p.PlayerId == firstPlayerInRound.PlayerId).PlayerGuid,
                RoundNumber = firstPlayerInRound.RoundNumber
            });
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception adding Campaign Turns to Game.";
            logger.LogError(ex, error);
            return ServiceResult<FirstPlayerInRound>.Fail([error, ex.Message]);
        }

    }

    private class DTActivePlayer
    {
        public int GameId { get; set; }
        public int PlayerId { get; set; }
        public Guid PlayerGuid { get; set; }
    }
}
