using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Handlers.Turns;

public record StartTurnOnRosterRequest(
    Guid GameGuid,
    Guid PlayerGuid,
    int RoundNumber
) : IRequest<ServiceResultBase>;

internal class StartTurnOnRosterRequestValidator : AbstractValidator<StartTurnOnRosterRequest>
{
    public StartTurnOnRosterRequestValidator()
    {
        RuleFor(x => x.GameGuid)
            .NotEmpty().WithMessage("GameGuid is required.");
        RuleFor(r => r.GameGuid)
         .Must(guid => guid != Guid.Empty)
         .WithMessage("GameGuid must be a valid non-empty GUID.");

        RuleFor(x => x.PlayerGuid)
            .NotEmpty().WithMessage("PlayerGuid is required.");
        RuleFor(r => r.PlayerGuid)
         .Must(guid => guid != Guid.Empty)
         .WithMessage("PlayerGuid must be a valid non-empty GUID.");
    }
}

internal class StartTurnOnRosterHandler(
    ILogger<StartTurnOnRosterHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<StartTurnOnRosterRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(StartTurnOnRosterRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new StartTurnOnRosterRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(StartTurnOnRosterRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResultBase.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Get the PlayerId
            DTIds? ids = await context.Players
                .AsNoTracking()
                .Where(p => p.Guid == request.PlayerGuid)
                .Where(p => p.Game.GameGuid == request.GameGuid)
                .Where(p => p.Status == PlayerStatus.Active)
                .Select(p => new DTIds()
                {
                    PlayerId = p.Id,
                    GameId = p.Game.Id
                })
                .FirstOrDefaultAsync(cancelToken);

            if (ids == null)
            {
                logger.LogWarning("Player with Guid {PlayerGuid} in Game {GameGuid} not found or not active.", request.PlayerGuid, request.GameGuid);
                return ServiceResultBase.Fail("Player not found or not active in the specified game.");
            }

            TurnRoster? turnRoster = await context.TurnRosters
                .Where(r => r.GameId == ids.GameId)
                .Where(r => r.PlayerId == ids.PlayerId)
                .Where(r => r.RoundNumber == request.RoundNumber)
                .SingleOrDefaultAsync(cancelToken);

            if (turnRoster == null)
            {
                logger.LogWarning("Turn Roster entry not found for PlayerId {PlayerId} in GameId {GameId} for Round {RoundNumber}.", ids.PlayerId, ids.GameId, request.RoundNumber);
                return ServiceResultBase.Fail("Turn Roster entry not found for the specified player and round.");
            }

            turnRoster.Status = TurnStatus.TakingTurn;
            turnRoster.TurnStartTime = DateTimeOffset.Now;
            turnRoster.ScheduledTurnEndTime = DateTimeOffset.Now.AddHours(48);

           await context.SaveChangesAsync(CancellationToken.None);
           
            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while processing StartTurnOnRosterRequest.");
            return ServiceResultBase.Fail("An internal error occurred while processing the request.");
        }
    }

    public class DTIds
    {
        public int PlayerId { get; set; }
        public int GameId { get; set; }
    }
}
