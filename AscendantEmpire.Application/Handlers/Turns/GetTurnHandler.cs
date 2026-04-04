using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Turns;

public record GetTurnRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<RosterEntry>>>;

internal class GetTurnRequestValidator : AbstractValidator<GetTurnRequest>
{
    public GetTurnRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

internal class GetTurnHandler(
    ILogger<GetTurnHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
    ) : IRequestHandler<GetTurnRequest, ServiceResult<IReadOnlyCollection<RosterEntry>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<RosterEntry>>> Handle(GetTurnRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetTurnRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTurnRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<RosterEntry>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            var q =
                from r in context.TurnRosters.AsNoTracking()
                where r.Status == TurnStatus.TakingTurn
                   && r.Game.Id == request.GameId
                   && r.Player.Status == PlayerStatus.Active

                // Ensures we only return lanes (campaign/battle) where THIS player is on that roster.
                join my in context.TurnRosters.AsNoTracking()
                    on new { r.GameId, r.RoundNumber, r.BattleId }
                    equals new { my.GameId, my.RoundNumber, my.BattleId }
                where my.Player.Id == request.PlayerId
                   && my.Player.Status == PlayerStatus.Active

                let scheduledEnd = r.ScheduledTurnEndTime
                    ?? r.TurnEndTime
                    ?? r.TurnStartTime
                    ?? r.CreatedAt

                select new TurnRosterSnapshotDb(
                    r.PlayerId,
            
                    // Count ALSO includes the player currently taking their turn.
            my.TurnOrder == r.TurnOrder
                ? 0
                : my.TurnOrder > r.TurnOrder
                    ? 1 + (from x in context.TurnRosters.AsNoTracking()
                           where x.GameId == r.GameId
                              && x.RoundNumber == r.RoundNumber
                              && x.BattleId == r.BattleId
                              && x.Player.Status == PlayerStatus.Active
                              && x.TurnOrder > r.TurnOrder
                              && x.TurnOrder < my.TurnOrder
                           select x.Id).Count()
                    : 1 + (from x in context.TurnRosters.AsNoTracking()
                           where x.GameId == r.GameId
                              && x.RoundNumber == r.RoundNumber
                              && x.BattleId == r.BattleId
                              && x.Player.Status == PlayerStatus.Active
                              && (x.TurnOrder > r.TurnOrder || x.TurnOrder < my.TurnOrder)
                           select x.Id).Count(),
                    r.BattleId == null,
                    scheduledEnd,
                    r.RoundNumber
                );

            TurnRosterSnapshotDb[] rows = await q.ToArrayAsync(cancelToken);

            if (rows.Length == 0)
            {
                const string error = "No records found for the specified Game and Player values.";
                logger.LogWarning(error + " Game Id: {g1}, Player Id: {g2}", request.GameId, request.PlayerId);
                return ServiceResult<IReadOnlyCollection<RosterEntry>>.NotFound(error);
            }

            var now = DateTimeOffset.UtcNow;

            RosterEntry[] rosters = [.. rows.Select(x => new RosterEntry
            {
                CurrentTurnPlayerId = x.CurrentTurnPlayerId,
                NumberOfPlayersUntilMyTurn = x.NumberOfPlayersUntilMyTurn,
                IsCampaign = x.IsCampaign,
                MaxTimeRemainingUntilTurn = x.ScheduledEnd - now, // <-- in memory
                RoundNumber = x.RoundNumber
            })];

            return ServiceResult<IReadOnlyCollection<RosterEntry>>.Succeed(rosters);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while processing {nameof(GetTurnRequest)}.";
            logger.LogError(ex, "{Error} Exception: {ExceptionMessage}", error, ex.Message);
            return ServiceResult<IReadOnlyCollection<RosterEntry>>.Fail([error, ex.Message]);
        }
    }

    private sealed record TurnRosterSnapshotDb(
        int CurrentTurnPlayerId,
        int NumberOfPlayersUntilMyTurn,
        bool IsCampaign,
        DateTimeOffset ScheduledEnd,
        int RoundNumber
    );
}