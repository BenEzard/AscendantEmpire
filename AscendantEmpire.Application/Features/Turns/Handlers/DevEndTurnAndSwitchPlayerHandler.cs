using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

// Remove in F-18. Temporary developer convenience so that one person can play every seat in a Game without re-entering credentials. The whole file goes
// when F-18 is done.

/// <summary>
/// Ends the calling Player's Campaign Turn and reports whoever is playing afterwards, so the caller can be logged straight in as them.
/// </summary>
/// <remarks>
/// <para>Remove in F-18. This exists only so that a single developer can play a multi-player Game on their own: it performs the Turn hand-over and hands
/// back everything needed to mint a session for the next Player, with no credential check of any kind.</para>
/// <para>With <paramref name="CycleWholeRound"/> false it advances by exactly one Turn. With it true it keeps ending Turns until play has come back round
/// to <paramref name="PlayerId"/> — "end round, back to this Player". The Players passed over that way are never logged in; their Turns are ended
/// directly, because ending the Turn is the whole point and a token for them would only be thrown away.</para>
/// </remarks>
/// <param name="GameId">The Game the caller has entered, taken from their game token.</param>
/// <param name="PlayerId">The Player the caller controls, taken from their game token. This is the Player whose Turn ends first.</param>
/// <param name="CycleWholeRound">End every Turn until it is <paramref name="PlayerId"/>'s Turn again, rather than stopping at the next Player.</param>
public record DevEndTurnAndSwitchPlayerRequest(
    int GameId,
    int PlayerId,
    bool CycleWholeRound
) : IRequest<ServiceResult<DevSwitchedPlayer>>;

/// <summary>
/// Who is playing once the developer end-turn has run, with the display detail needed to reopen the Game as them.
/// </summary>
/// <remarks>
/// Remove in F-18. The display fields duplicate what the Open Game dialog would have supplied, because this switch bypasses that dialog entirely — the
/// client never gets a row to read them off.
/// </remarks>
public record DevSwitchedPlayer
{
    /// <summary>
    /// The identity to issue a game token for: the account behind the Player now taking their Turn.
    /// </summary>
    public GameContext GameContext { get; init; } = null!;

    public string GameName { get; init; } = string.Empty;

    public int MapWidth { get; init; }

    public int MapHeight { get; init; }

    /// <summary>
    /// The new Player's ruler label, in the same "Title Name (Faction)" shape the Open Game listing uses.
    /// </summary>
    public string RulerLabel { get; init; } = string.Empty;

    public int CampaignOffsetId { get; init; }

    /// <summary>
    /// How many Turns were ended to get here: one for a single step, one per Player for a whole Round.
    /// </summary>
    public int TurnsEnded { get; init; }

    /// <summary>
    /// True when at least one of those Turns closed the Round.
    /// </summary>
    public bool RoundEnded { get; init; }
}

internal class DevEndTurnAndSwitchPlayerRequestValidator : Validator<DevEndTurnAndSwitchPlayerRequest>
{
    public DevEndTurnAndSwitchPlayerRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

internal class DevEndTurnAndSwitchPlayerHandler(
    ILogger<DevEndTurnAndSwitchPlayerHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMediator mediator,
    ICampaignTurnService campaignTurnService) : IRequestHandler<DevEndTurnAndSwitchPlayerRequest, ServiceResult<DevSwitchedPlayer>>
{
    /// <summary>
    /// Stops a cycle that is not converging, so a Queue in an unexpected state cannot spin here indefinitely.
    /// </summary>
    /// <remarks>
    /// A whole Round ends one Turn per active Participant, so the Participant count is the real bound. This is the backstop for the case where ending a
    /// Turn somehow fails to move the Queue on at all.
    /// </remarks>
    private const int _MAX_TURNS_TO_END = 100;

    public async Task<ServiceResult<DevSwitchedPlayer>> Handle(DevEndTurnAndSwitchPlayerRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validateRequest = new DevEndTurnAndSwitchPlayerRequestValidator();
            ValidationResult validationResult = validateRequest.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(DevEndTurnAndSwitchPlayerRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<DevSwitchedPlayer>.Fail(errors.Prepend(error));
            }

            // End the Turns. The ordinary end-turn request does all the real work — the pipelines, the Queue stamping, and starting whoever plays next —
            // so this only decides how many times to ask for it, and on whose behalf.
            ServiceResult<DevTurnCycleOutcome> cycleResult = await EndTurnsAsync(request, cancelToken);

            if (cycleResult.HasSucceeded == false || cycleResult.SuccessValue is null)
            {
                return ServiceResult<DevSwitchedPlayer>.Fail(cycleResult.Errors);
            }

            // Then describe whoever is now up, in enough detail for the client to reopen the board as them.
            return await DescribeCurrentPlayerAsync(request.GameId, cycleResult.SuccessValue, cancelToken);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while processing {nameof(DevEndTurnAndSwitchPlayerRequest)}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResult<DevSwitchedPlayer>.InternalServerError(error);
        }
    }

    /// <summary>
    /// What the end-turn loop settled on: who is playing now, and what it took to get there.
    /// </summary>
    private record DevTurnCycleOutcome(int CurrentPlayerId, int TurnsEnded, bool RoundEnded);

    /// <summary>
    /// Ends one Turn, or every Turn up to the one that brings play back round to the caller.
    /// </summary>
    /// <remarks>
    /// Every Turn is ended with <see cref="EndTurnMode.ForceEnd"/>. The check-first mode exists to ask a Player whether they have finished, and there is
    /// nobody to ask here — a developer pressing this button has already decided.
    /// </remarks>
    private async Task<ServiceResult<DevTurnCycleOutcome>> EndTurnsAsync(DevEndTurnAndSwitchPlayerRequest request, CancellationToken cancelToken)
    {
        int currentPlayerId = request.PlayerId;
        int turnsEnded = 0;
        bool roundEnded = false;

        while (true)
        {
            ServiceResult<EndCampaignTurnOutcome> endTurnResult = await mediator.Send(
                new EndCampaignTurnRequest(request.GameId, currentPlayerId, EndTurnMode.ForceEnd),
                cancelToken);

            if (endTurnResult.HasSucceeded == false || endTurnResult.SuccessValue is null)
            {
                string error = $"Failed to end the Turn for Player Id {currentPlayerId} in Game Id {request.GameId}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", endTurnResult.Errors));

                // The end-turn errors are the useful ones — "it is not that Player's turn", and so on — so they travel with ours rather than being
                // replaced by it.
                return ServiceResult<DevTurnCycleOutcome>.Fail(endTurnResult.Errors.Prepend(error));
            }

            turnsEnded++;
            roundEnded = roundEnded || endTurnResult.SuccessValue.RoundEnded;

            // Ending a Turn also starts the next one, so the Queue already knows who is up.
            ServiceResult<CurrentCampaignTurn> currentTurnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

            if (currentTurnResult.HasSucceeded == false || currentTurnResult.SuccessValue is null)
            {
                const string error = "The Turn ended, but the Player now taking their Turn could not be determined.";
                logger.LogWarning("{Error} Game Id {GameId}. Errors: {Errors}", error, request.GameId, string.Join("; ", currentTurnResult.Errors));

                return ServiceResult<DevTurnCycleOutcome>.Fail(currentTurnResult.Errors.Prepend(error));
            }

            currentPlayerId = currentTurnResult.SuccessValue.PlayerId;

            // A single step is finished as soon as one Turn has ended, whoever that landed on.
            if (request.CycleWholeRound == false)
            {
                break;
            }

            // A whole Round is finished when play has come back round to the caller. A single-Player Game satisfies this on the first pass, which is
            // correct: their Turn ended, the Round closed, and it is their Turn again.
            if (currentPlayerId == request.PlayerId)
            {
                break;
            }

            if (turnsEnded >= _MAX_TURNS_TO_END)
            {
                string error = $"Gave up cycling Turns in Game Id {request.GameId} after {turnsEnded} Turns without returning to Player Id {request.PlayerId}.";
                logger.LogError("{Error}", error);

                return ServiceResult<DevTurnCycleOutcome>.Fail(error);
            }
        }

        logger.LogInformation("Developer end turn ended {TurnsEnded} Turn(s) in Game Id {GameId}. Player Id {PlayerId} is now taking their Turn.",
            turnsEnded, request.GameId, currentPlayerId);

        return ServiceResult<DevTurnCycleOutcome>.Succeed(new DevTurnCycleOutcome(currentPlayerId, turnsEnded, roundEnded));
    }

    /// <summary>
    /// Builds the switch result for the Player now taking their Turn: their account, their permissions, and the board detail to redraw with.
    /// </summary>
    /// <remarks>
    /// An unassigned slot is refused rather than worked around. There is no account to log in as, and leaving the caller logged in as the Player whose
    /// Turn has just ended would hand them a board they cannot act on.
    /// </remarks>
    private async Task<ServiceResult<DevSwitchedPlayer>> DescribeCurrentPlayerAsync(int gameId, DevTurnCycleOutcome cycle, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        var playerRow = await context.Players
            .AsNoTracking()
            .Where(p => p.Id == cycle.CurrentPlayerId)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                p.RulerTitle,
                p.RulerName,
                p.FactionName,
                p.CampaignOffsetId,
                p.Game.GameName,
                p.Game.MapWidth,
                p.Game.MapHeight
            })
            .SingleOrDefaultAsync(cancelToken);

        if (playerRow is null)
        {
            string error = $"Player Id {cycle.CurrentPlayerId} was not found.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<DevSwitchedPlayer>.NotFound(error);
        }

        if (playerRow.UserId is null)
        {
            string error = $"Player Id {cycle.CurrentPlayerId} has no user account, so there is nobody to log in as.";
            logger.LogWarning("{Error} Game Id {GameId}.", error, gameId);

            return ServiceResult<DevSwitchedPlayer>.Fail(error);
        }

        int userId = playerRow.UserId.Value;

        string username = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstAsync(cancelToken);

        // Permissions belong to the account, so the new game token carries whatever that account holds — not whatever the caller held.
        IReadOnlyCollection<PermissionType> permissions = await context.GetUserPermissionsAsync(userId, cancelToken);

        return ServiceResult<DevSwitchedPlayer>.Succeed(new DevSwitchedPlayer
        {
            GameContext = new GameContext(userId, username, gameId, playerRow.Id, permissions),
            GameName = playerRow.GameName,
            MapWidth = playerRow.MapWidth,
            MapHeight = playerRow.MapHeight,
            RulerLabel = $"{playerRow.RulerTitle} {playerRow.RulerName} ({playerRow.FactionName})",
            CampaignOffsetId = playerRow.CampaignOffsetId,
            TurnsEnded = cycle.TurnsEnded,
            RoundEnded = cycle.RoundEnded
        });
    }
}
