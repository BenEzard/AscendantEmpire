using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record EndTurnOnRosterRequest(
    int GameId,
    int RoundNumber,
    int CurrentTurnPlayerId,
    int? BattleId
) : IRequest<ServiceResultBase>;

internal class EndTurnOnRosterRequestValidator : AbstractValidator<EndTurnOnRosterRequest>
{
    public EndTurnOnRosterRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("GameId must be greater than 0.");

        RuleFor(x => x.RoundNumber)
            .GreaterThan(0).WithMessage("RoundNumber must be greater than 0.");

        RuleFor(x => x.CurrentTurnPlayerId)
            .GreaterThan(0).WithMessage("CurrentPlayerTurnId must be greater than 0.");
    }
}

/// <summary>
/// Ends a Player's Turn on the Roster
/// </summary>
/// <remarks>WARNING: This method assumes that validation of the information passed to it has ALREADY BEEN CONFIRMED.</remarks>
internal class EndTurnOnRosterHandler(
    ILogger<EndTurnOnRosterHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<EndTurnOnRosterRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(EndTurnOnRosterRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new EndTurnOnRosterRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(EndTurnOnRosterRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResultBase.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            var currentRoster = await context.TurnRosters
                .Where(r => r.GameId == request.GameId)
                .Where(r => r.RoundNumber == request.RoundNumber)
                .Where(r => r.BattleId == request.BattleId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    CurrentRosterEntry = g
                        .Where(r => r.PlayerId == request.CurrentTurnPlayerId)
                        .Where(r => r.Status == TurnStatus.TakingTurn)
                        .SingleOrDefault(),

                    NextPendingPlayerGuid =
                        g.Where(r => r.Status == TurnStatus.Pending)
                         .Where(r => r.TurnOrder > g
                             .Where(x => x.PlayerId == request.CurrentTurnPlayerId && x.Status == TurnStatus.TakingTurn)
                             .Select(x => (int?)x.TurnOrder)
                             .SingleOrDefault())
                         .OrderBy(r => r.TurnOrder)
                         .Select(r => (int?)r.PlayerId)
                         .FirstOrDefault()
                })
                .SingleOrDefaultAsync(CancellationToken.None);

            if (currentRoster is null)
            {
                string error = $"No current roster found with a Player taking a Turn.";
                logger.LogWarning("{method} {err}: ", nameof(EndTurnOnRosterRequest), error);
                return ServiceResultBase.Fail(error);
            }

            TurnRoster? currentRosterEntry = currentRoster.CurrentRosterEntry;

            if (currentRosterEntry == null)
            {
                string error = $"No current roster entry found for the specified Player in the Game.";
                logger.LogWarning("{method} {err}: ", nameof(EndTurnOnRosterRequest), error);
                return ServiceResultBase.Fail(error);
            }
            else
            {
                // End the current Turn
                currentRosterEntry.Status = TurnStatus.TurnCompleted;
                currentRosterEntry.TurnEndTime = DateTimeOffset.UtcNow;

                await context.SaveChangesAsync(CancellationToken.None);

                if (currentRoster.NextPendingPlayerGuid is not null)
                {
                    // Start the next Player's turn
                    await mediator.Send(new StartTurnOnRosterRequest(
                        GameId: request.GameId,
                        PlayerId: currentRoster.NextPendingPlayerGuid.Value,
                        RoundNumber: currentRosterEntry.RoundNumber),
                        CancellationToken.None);
                }
                else
                {
                    // End the Round
                    ServiceResultBase endRoundResult = await mediator.Send(new EndRoundRequest(GameId: request.GameId), CancellationToken.None);

                    if (endRoundResult.HasSucceeded == false)
                    {
                        string error = $"Failed to End the Round.";
                        logger.LogWarning("{method}: {Error} Errors: {Errors}", nameof(EndTurnOnRosterHandler), error, string.Join("; ", endRoundResult.Errors));
                        return ServiceResultBase.Fail(endRoundResult.Errors.Prepend(error));
                    }
                }

                return ServiceResultBase.Succeed();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while processing {method}", nameof(EndTurnOnRosterRequest));
            return ServiceResultBase.Fail("An unhandled exception occurred.");

        }
    }
}
