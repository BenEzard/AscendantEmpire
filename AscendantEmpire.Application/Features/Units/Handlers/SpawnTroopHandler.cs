using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Helpers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Entities.Units;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Units.Handlers;

public record SpawnTroopRequest(
    int GameId,
    int PlayerId,
    int XCoord,
    int YCoord,
    int UnitDesignId,
    int InitialStrength,
    int HealthyCount,
    int InjuredCount
) : IRequest<ServiceResultBase>;

internal class SpawnTroopRequestValidator : AbstractValidator<SpawnTroopRequest>
{
    public SpawnTroopRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.XCoord)
            .GreaterThanOrEqualTo(0).WithMessage("XCoord must be greater than or equal to 0.");

        RuleFor(x => x.YCoord)
            .GreaterThanOrEqualTo(0).WithMessage("YCoord must be greater than or equal to 0.");

        RuleFor(x => x.UnitDesignId)
            .GreaterThan(0).WithMessage("Unit Design Id must be greater than 0.");

        RuleFor(x => x.InitialStrength)
            .GreaterThan(0).WithMessage("Initial Strength must be greater than 0.");

        RuleFor(x => x.HealthyCount)
            .GreaterThan(0).WithMessage("Healthy Count must be greater than 0.");

        RuleFor(x => x.InjuredCount)
            .GreaterThanOrEqualTo(0).WithMessage("Injured Count must be greater than or equal to 0.");
    }
}

internal class SpawnTroopHandler(
    ILogger<SpawnTroopHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<SpawnTroopRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SpawnTroopRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new SpawnTroopRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(SpawnTroopRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResultBase.Fail(errors.Prepend(error));
            }

            // Load the game info
            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (gameInfoResult.HasSucceeded == false)
            {
                const string error = "Error getting game info while trying to Spawn a Unit.";
                logger.LogWarning(error);
                return ServiceResultBase.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

            if (gameInfo == null)
            {
                const string error = "Game not found while trying to Spawn a Unit.";
                logger.LogWarning(error);
                return ServiceResultBase.NotFound(error);
            }

            OpenGamePlayer thisPlayer = gameInfo.Players.Single();

            cancelToken.ThrowIfCancellationRequested();

            // Convert XCoord and YCoord to a single index based on the map size
            // Set with a mapOffset of 1, so that it is the WORLD coordinates, not the Player's coordinates
            int campaignMapTileId = MapMethods.GetDbId(1, gameInfo.MapWidth, request.XCoord, request.YCoord);

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            context.TroopInstances.Add(new TroopInstance
            {
                GameId = gameInfo.GameId,
                PlayerId = thisPlayer.PlayerId,
                CampaignMapTileId = campaignMapTileId,
                UnitDesignId = request.UnitDesignId,
                InitialStrength = request.InitialStrength,
                HealthyCount = request.HealthyCount,
                InjuredCount = request.InjuredCount,
            });

            await context.SaveChangesAsync(cancelToken);
    
            return ServiceResultBase.Created();
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(SpawnTroopHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResultBase.Fail(error);
        }
    }
}
