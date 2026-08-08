using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Units.GetTroopDisplayInfo;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Units.Handlers;

public record GetTroopDisplayInfoRequest(
    int GameId
) : IRequest<ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>>;

internal class GetTroopDisplayInfoRequestValidator : AbstractValidator<GetTroopDisplayInfoRequest>
{
    public GetTroopDisplayInfoRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class GetTroopDisplayInfoHandler(
    ILogger<GetTroopDisplayInfoHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTroopDisplayInfoRequest, ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>> Handle(GetTroopDisplayInfoRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetTroopDisplayInfoRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTroopDisplayInfoRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>.Fail(errors.Prepend(error));
            }

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, null), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for Game Id: {request.GameId}.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>.NotFound(error);
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A template's map image comes from its compiled snapshot. Include every template a player in this
            // game could field: global/common templates unlocked (and not obsolete) by a player in the game,
            // plus custom templates owned by a player in the game (custom templates carry no unlock rows —
            // they are built from already-unlocked components).
            List<TroopDisplayInfo> troopDisplayInfo = await context.CompiledUnitTemplates
                .Where(c => c.UnitTemplate.GameId == null || c.UnitTemplate.GameId == request.GameId)
                .Where(c => c.UnitTemplate.PlayerStates
                        .Any(ps => ps.Player.GameId == request.GameId && ps.ObsoleteOnTurn == null)
                    || (c.UnitTemplate.PlayerId != null && c.UnitTemplate.Player!.GameId == request.GameId))
                .Select(c => new TroopDisplayInfo
                {
                    UnitTemplateId = c.UnitTemplateId,
                    MapImagePath = c.MapImagePath
                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>.Succeed(troopDisplayInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured while handling {RequestName}", nameof(GetTroopDisplayInfoRequest));
            return ServiceResult<IReadOnlyCollection<TroopDisplayInfo>>.Fail("An unexpected error occurred while retrieving troop display information.");
        }
    }
}
