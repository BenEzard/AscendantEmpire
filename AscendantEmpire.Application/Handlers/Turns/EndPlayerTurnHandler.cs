using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.Domain.Entities.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Turns;

public record EndPlayerTurnRequest(
    Guid GameGuid,
    Guid PlayerGuid
) : IRequest<ServiceResultBase>;

internal class EndPlayerTurnRequestValidator : AbstractValidator<EndPlayerTurnRequest>
{
    public EndPlayerTurnRequestValidator()
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

/// <summary>
/// Handles requests to end a Player's turn within a Game.
/// </summary>
/// <remarks>
/// This handler ensures that the request is valid, verifies that it is the specified player's turn, and
/// updates the game state accordingly.
/// </remarks>
internal class EndPlayerTurnHandler(
    ILogger<EndPlayerTurnHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<EndPlayerTurnRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(EndPlayerTurnRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new EndPlayerTurnRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(EndPlayerTurnRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return Fail(errors.Prepend(error));
            }

            // Confirm the game exists, and it's the Player's turn
            ServiceResult<CurrentTurnRosterInfo> currentTurnResult = await ConfirmItsPlayersTurnAsync(request, cancelToken);

            if (currentTurnResult.HasSucceeded == false)
            {
                return currentTurnResult;
            }

            CurrentTurnRosterInfo currentTurnInfo = currentTurnResult.SuccessValue!;

            // End the Player's Turn on the Roster
            ServiceResultBase endPlayerResult = await mediator.Send(new EndTurnOnRosterRequest(
                GameId: currentTurnInfo.GameId,
                GameGuid: currentTurnInfo.GameGuid,
                RoundNumber: currentTurnInfo.RoundNumber,
                CurrentTurnPlayerId: currentTurnInfo.CurrentTurnPlayerId,
                BattleId: null
            ), cancelToken);

            if (endPlayerResult.HasSucceeded == false)
            {
                string error = $"Failed to end turn for player {request.PlayerGuid} in game {request.GameGuid}.";
                logger.LogWarning("{Error} Details: {Details}", error, string.Join("; ", endPlayerResult.Errors));
                return Fail(endPlayerResult.Errors.Prepend(error));
            }

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            string error = $"An exception occurred while processing {nameof(EndPlayerTurnRequest)}.";
            logger.LogError(ex, error);
            return Fail(error);
        }
    }

    /// <summary>
    /// Validates that it is currently the specified Player's turn in the Game before allowing the turn to be ended.
    /// </summary>
    /// <remarks>
    /// Validates:
    /// <list type="bullet">
    /// <item>The Player is in the specified Game</item>
    /// <item>Current turn information exists for the Player</item>
    /// <item>It is the Player's turn</item>
    /// </list>
    /// </remarks>
    /// <returns>The Current Turn Roster Information if validation succeeds; otherwise, a failure ServiceResult with error details.</returns>
    private async Task<ServiceResult<CurrentTurnRosterInfo>> ConfirmItsPlayersTurnAsync(EndPlayerTurnRequest request, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Get the current Turn information to verify it's the Player's turn
        ServiceResult<IReadOnlyCollection<RosterEntry>> turnResult = await mediator.Send(new GetTurnRequest(GameGuid: request.GameGuid, PlayerGuid: request.PlayerGuid), cancelToken);

        if (turnResult.HasSucceeded == false)
        {
            string error = $"Failed to retrieve current turn information for {nameof(EndPlayerTurnHandler)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", turnResult.Errors));
            return ServiceResult<CurrentTurnRosterInfo>.Fail(turnResult.Errors.Prepend(error));
        }

        if (turnResult.SuccessValue?.Count == 0)
        {
            const string error = "Player not found for the specified Game.";
            logger.LogWarning("{method}: " + error, nameof(EndPlayerTurnHandler));
            return ServiceResult<CurrentTurnRosterInfo>.Fail(error);
        }

        // Get the Campaign Roster Entry
        RosterEntry? currentTurnInfo = turnResult.SuccessValue?.FirstOrDefault(tr => tr.IsCampaign);

        if (currentTurnInfo == null)
        {
            const string error = "No current turn information found for the specified Player.";
            logger.LogWarning("{method}: {err}", nameof(EndPlayerTurnHandler), error);
            return ServiceResult<CurrentTurnRosterInfo>.Fail(error);
        }
        // Check it's the Player's turn
        else if (currentTurnInfo != null && currentTurnInfo.NumberOfPlayersUntilMyTurn != 0)
        {
            string error = $"It's not the specified Player's turn.";
            logger.LogWarning("{method}: {Error}", nameof(EndPlayerTurnHandler), error);
            return ServiceResult<CurrentTurnRosterInfo>.Fail(error);
        }

        // Get the Game Id
        GameIndex? game = await context.GameIndex.Where(g => g.GameGuid == request.GameGuid).SingleOrDefaultAsync(CancellationToken.None);

        if (game == null)
        {
            string error = $"Game not found for the specified GameGuid.";
            logger.LogWarning("{method} {err}: ", nameof(EndTurnOnRosterRequest), error);
            return ServiceResult<CurrentTurnRosterInfo>.Fail(error);
        }

        return ServiceResult<CurrentTurnRosterInfo>.Succeed(new CurrentTurnRosterInfo
        (
            GameId: game.Id,
            GameGuid: request.GameGuid,
            RoundNumber: currentTurnInfo!.RoundNumber,
            CurrentTurnPlayerId: currentTurnInfo.CurrentTurnPlayerId
        ));
    }

    public record CurrentTurnRosterInfo 
    (
        int GameId,
        Guid GameGuid,
        int RoundNumber,
        int CurrentTurnPlayerId
    );
}
