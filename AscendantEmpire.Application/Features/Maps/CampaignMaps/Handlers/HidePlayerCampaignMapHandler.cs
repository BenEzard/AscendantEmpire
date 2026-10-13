using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;

public record HidePlayerCampaignMapRequest(int GameId, int PlayerId)
    : IRequest<ServiceResultBase>;

internal class HidePlayerCampaignMapRequestValidator : Validator<HidePlayerCampaignMapRequest>
{
    public HidePlayerCampaignMapRequestValidator()
    {
        RuleFor(r => r.GameId)
             .GreaterThan(0).WithMessage("Game Id is required.");
        RuleFor(r => r.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

internal class HidePlayerCampaignMapHandler(
    ILogger<HidePlayerCampaignMapHandler> logger,
    IMediator mediator,
    AscendantEmpireContext context
    ) : IRequestHandler<HidePlayerCampaignMapRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(
        HidePlayerCampaignMapRequest request,
        CancellationToken cancelToken)
    {
        // Validate the request object
        var validator = new HidePlayerCampaignMapRequestValidator();

        if (validator.Validate(request) is not { IsValid: true })
        {
            var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
            const string error = "Validation failed for HidePlayerCampaignMapRequest.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(errors.Prepend(error));
        }

        // Load the game info
        ServiceResult<IReadOnlyCollection<GameContext>> gameInfoResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancelToken);

        if (gameInfoResult.HasSucceeded == false)
        {
            const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(gameInfoResult.Errors.Prepend(error));
        }

        GameContext? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

        if (gameInfo == null)
        {
            const string error = "Game not found while trying to get a Player's view of the Campaign Map";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.NotFound(error);
        }

        GameContextPlayer thisPlayer = gameInfo.Players.Single();

        // Update the PlayerCampaignMap entries to hide the map details
        await context.PlayerCampaignMaps
            .Where(pcm => pcm.GameId == gameInfo.GameId)
            .Where(pcm => pcm.PlayerId == thisPlayer.PlayerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(pcm => pcm.BiomeTypeId, BiomeType.Unknown)
                    .SetProperty(pcm => pcm.CampaignMapGeographyTypeId, CampaignMapGeographyType.Unknown)
                    .SetProperty(pcm => pcm.FirstSeenOnTurnId, (int?)null)
                    .SetProperty(pcm => pcm.LastSeenOnTurnId, (int?)null),
                cancelToken);

        // Update the CampaignMap to remove any seen tiles for this player
        await context.CampaignMaps
           .Where(pcm => pcm.GameId == gameInfo.GameId)
           .Where(pcm => pcm.FirstSeenByPlayerId == thisPlayer.PlayerId)
           .ExecuteUpdateAsync(
               setters => setters
                   .SetProperty(pcm => pcm.FirstSeenOnTurnId, (int?)null)
                   .SetProperty(pcm => pcm.FirstSeenByPlayerId, (int?)null),
               cancelToken);

        await context.CampaignMaps
           .Where(pcm => pcm.GameId == gameInfo.GameId)
           .Where(pcm => pcm.LastVisitedByPlayerId == thisPlayer.PlayerId)
           .ExecuteUpdateAsync(
               setters => setters
                   .SetProperty(pcm => pcm.LastVisitedOnTurnId, (int?)null)
                   .SetProperty(pcm => pcm.LastVisitedByPlayerId, (int?)null),
               cancelToken);

        return await mediator.Send(new GetPlayerCampaignMapRequest(request.GameId, request.PlayerId, LoadWholeMap: true), cancelToken);

    }
}
