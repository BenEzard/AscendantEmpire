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

            List<TroopDisplayInfo> troopDisplayInfo = await context.UnitDesignStats
                .Where(t => t.UnitDesign.GameId == null || t.UnitDesign.GameId == request.GameId)
                .Where(t => t.UnitDesign.PlayerStates
                    .Any(ps => ps.Player.GameId == request.GameId && ps.IsUnlocked && !ps.IsObsolete))
                .Select(t => new
                {
                    t.UnitDesignId,
                    t.UnitDesign.MapImagePath
                })
                .Distinct()
                .Select(t => new TroopDisplayInfo
                {
                    UnitDesignId = t.UnitDesignId,
                    MapImagePath = t.MapImagePath
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
