using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Units;

public record TrainTroopRequest(
    Guid GameGuid,
    Guid PlayerGuid,
    int XCoord,
    int YCoord,
    int SightRange
) : IRequest<ServiceResultBase>;

internal class TrainTroopRequestValidator : AbstractValidator<TrainTroopRequest>
{
    public TrainTroopRequestValidator()
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

        RuleFor(x => x.XCoord)
            .GreaterThanOrEqualTo(0).WithMessage("XCoord must be greater than or equal to 0.");

        RuleFor(x => x.YCoord)
            .GreaterThanOrEqualTo(0).WithMessage("YCoord must be greater than or equal to 0.");

        RuleFor(x => x.SightRange)
            .GreaterThanOrEqualTo(-1).WithMessage("SightRange must be greater than or equal to -1.");
    }
}

internal class TrainTroopHandler(
    ILogger<TrainTroopHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<TrainTroopRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(TrainTroopRequest request, CancellationToken cancelToken)
    {
        //try
        //{
        //    // Validate the request object
        //    var validator = new TrainTroopRequestValidator();

        //    if (validator.Validate(request) is not { IsValid: true })
        //    {
        //        var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
        //        string error = $"Validation failed for {nameof(TrainTroopRequest)}.";
        //        logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
        //        return ServiceResultBase.Fail(errors.Prepend(error));
        //    }

        //    // Confirm the game exists, and it's the Player's turn
        //    ServiceResult currentTurnResult = await ConfirmItsPlayersTurnAsync(request, cancelToken);

        //    if (currentTurnResult.HasSucceeded == false)
        //    {
        //        return currentTurnResult;
        //    }

        //    CurrentTurnRosterInfo currentTurnInfo = currentTurnResult.SuccessValue!;


        //}
        //catch (Exception ex)
        //{
        //    string error = $"An exception occurred while processing {nameof(TrainTroopRequest)}.";
        //    logger.LogError(ex, error);
        //    return ServiceResultBase.Fail(error);
        //}

        return null; 
    }
}
