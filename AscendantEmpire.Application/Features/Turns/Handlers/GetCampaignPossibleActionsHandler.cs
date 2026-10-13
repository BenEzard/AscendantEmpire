using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;
using AscendantEmpire.FrontendContracts.Turns.GetCampaignPossibleActions;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record GetCampaignPossibleActionsRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<CampaignPossibleActions>>;

public record CampaignPossibleActions : ICampaignPossibleActions
{
    public bool IsYourTurn { get; set; }

    /// <summary>
    /// What the Player could still do before ending their Turn.
    /// </summary>
    public List<PossibleAction> PossibleActions { get; set; } = [];

    /// <summary>
    /// The contract's view of <see cref="PossibleActions"/>.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly so the serialized payload keeps the concrete list above, exactly as <c>EndCampaignTurnOutcome</c> does. The interface
    /// describes the wire shape to callers rather than reshaping what goes over it.
    /// </remarks>
    IReadOnlyCollection<IPossibleAction> ICampaignPossibleActions.PossibleActions => PossibleActions;
}

internal class GetCampaignPossibleActionsRequestValidator : Validator<GetCampaignPossibleActionsRequest>
{
    public GetCampaignPossibleActionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// Reads what a Player still has outstanding on the open Campaign Turn, without touching the Turn.
/// </summary>
/// <remarks>
/// <para>
/// The same checks an end-turn request runs, and nothing else. It exists because the end-turn route cannot be used to ask the question: in
/// <see cref="AscendantEmpire.Domain.Enums.Turns.EndTurnMode.CheckForActions"/> mode it <i>ends the Turn</i> when it finds nothing outstanding, so a client calling it
/// to populate a panel as the Game opens would end the Player's Turn for them.
/// </para>
/// <para>
/// Another Player's Turn, and a Game with no Turn open, both answer an empty list with <c>IsYourTurn</c> false rather than failing. Neither is a fault:
/// a Player may open their Game at any point in the Round, and being told to wait is the ordinary case in an asynchronous game.
/// </para>
/// </remarks>
internal class GetCampaignPossibleActionsHandler(
    ILogger<GetCampaignPossibleActionsHandler> logger,
    ICampaignTurnAccessService campaignTurnAccessService,
    IEndCampaignTurnPossibleActionService possibleActionService
) : IRequestHandler<GetCampaignPossibleActionsRequest, ServiceResult<CampaignPossibleActions>>
{
    /// <summary>Nobody's Turn to act on, so nothing is outstanding for the caller. Shared by both not-your-turn paths.</summary>
    private static ServiceResult<CampaignPossibleActions> NotYourTurn()
    {
        return ServiceResult<CampaignPossibleActions>.Succeed(new CampaignPossibleActions
        {
            IsYourTurn = false,
            PossibleActions = []
        });
    }

    public async Task<ServiceResult<CampaignPossibleActions>> Handle(GetCampaignPossibleActionsRequest request, CancellationToken cancelToken)
    {
        // Validate the request object
        var validator = new GetCampaignPossibleActionsRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (validationResult.IsValid == false)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetCampaignPossibleActionsRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<CampaignPossibleActions>.Fail(errors.Prepend(error));
        }

        CampaignTurnAccess access = await campaignTurnAccessService.GetCampaignTurnAccessAsync(request.GameId, request.PlayerId, cancelToken);

        switch (access.Status)
        {
            case CampaignTurnAccessStatus.PlayerNotInGame:
                // The one genuine refusal here. Asking about a Game you are not in is a mistake worth reporting, not an empty answer.
                return ServiceResult<CampaignPossibleActions>.NotFound(access.Errors);

            case CampaignTurnAccessStatus.NoTurnInProgress:
                return NotYourTurn();

            case CampaignTurnAccessStatus.Failed:
                return ServiceResult<CampaignPossibleActions>.Fail(access.Errors);
        }

        // Granted only says a Turn is open, so whose it is still has to be checked. Another Player's Turn is answered the same way as no Turn at all:
        // there is nothing this Player is being asked to do.
        CurrentCampaignTurn currentTurn = access.CurrentTurn!;

        if (currentTurn.PlayerId != request.PlayerId)
        {
            return NotYourTurn();
        }

        var possibleActionContext = new PossibleActionContext(request.GameId, request.PlayerId, currentTurn.CurrentRoundNumber);
        IReadOnlyList<PossibleAction> possibleActions = await possibleActionService.GetPossibleActionsAsync(possibleActionContext, cancelToken);

        return ServiceResult<CampaignPossibleActions>.Succeed(new CampaignPossibleActions
        {
            IsYourTurn = true,
            PossibleActions = [.. possibleActions]
        });
    }
}
