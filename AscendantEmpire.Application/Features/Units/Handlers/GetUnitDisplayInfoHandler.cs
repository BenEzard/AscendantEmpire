using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Units.GetUnitDisplayInfo;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Units.Handlers;

public record GetUnitDisplayInfoRequest(
    int GameId
) : IRequest<ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>>;

internal class GetUnitDisplayInfoRequestValidator : Validator<GetUnitDisplayInfoRequest>
{
    public GetUnitDisplayInfoRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class GetUnitDisplayInfoHandler(
    ILogger<GetUnitDisplayInfoHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetUnitDisplayInfoRequest, ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>> Handle(GetUnitDisplayInfoRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetUnitDisplayInfoRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetUnitDisplayInfoRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>.Fail(errors.Prepend(error));
            }

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, null), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for Game Id: {request.GameId}.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>.NotFound(error);
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A template's map image comes from its compiled snapshot. Include every template a player in this
            // game could field: global/common templates unlocked (and not obsolete) by a player in the game,
            // plus custom templates owned by a player in the game (custom templates carry no unlock rows —
            // they are built from already-unlocked components).
            List<UnitDisplayInfo> unitDisplayInfo = await context.CompiledUnitTemplates
                .Where(c => c.UnitTemplate.GameId == null || c.UnitTemplate.GameId == request.GameId)
                .Where(c => c.UnitTemplate.PlayerStates
                        .Any(ps => ps.Player.GameId == request.GameId && ps.ObsoleteOnTurn == null)
                    || (c.UnitTemplate.PlayerId != null && c.UnitTemplate.Player!.GameId == request.GameId))
                .Select(c => new UnitDisplayInfo
                {
                    UnitTemplateId = c.UnitTemplateId,
                    MapImagePath = c.MapImagePath,
                    DisplayName = c.DisplayName
                })
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>.Succeed(unitDisplayInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occured while handling {RequestName}", nameof(GetUnitDisplayInfoRequest));
            return ServiceResult<IReadOnlyCollection<UnitDisplayInfo>>.Fail("An unexpected error occurred while retrieving unit display information.");
        }
    }
}
