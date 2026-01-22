using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Maps;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps;

public record HidePlayerCampaignMapRequest(Guid GameGuid, Guid PlayerGuid)
    : IRequest<ServiceResultBase>;

internal class HidePlayerCampaignMapRequestValidator : AbstractValidator<HidePlayerCampaignMapRequest>
{
    public HidePlayerCampaignMapRequestValidator()
    {
        RuleFor(r => r.GameGuid)
             .NotEmpty().WithMessage("GameGuid is required.");
        RuleFor(r => r.PlayerGuid)
            .NotEmpty().WithMessage("PlayerGuid is required.");
        RuleFor(r => r.GameGuid)
            .Must(guid => guid != Guid.Empty)
            .WithMessage("GameGuid must be a valid non-empty GUID.");
        RuleFor(r => r.PlayerGuid)
            .Must(guid => guid != Guid.Empty)
            .WithMessage("PlayerGuid must be a valid non-empty GUID.");
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
        try
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
            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

            if (gameInfoResult.HasSucceeded == false)
            {
                const string error = "Error getting game info while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame? gameInfo = gameInfoResult.SuccessValue!.SingleOrDefault();

            if (gameInfo == null)
            {
                const string error = "Game not found while trying to get a Player's view of the Campaign Map";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<ObserverSight>>.NotFound(error);
            }

            OpenGamePlayer thisPlayer = gameInfo.Players.Single();

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

            return await mediator.Send(new GetPlayerCampaignMapRequest(request.GameGuid, request.PlayerGuid, Observers: null, LoadWholeMap: true), cancelToken);

        }
        catch (Exception ex)
        {
            const string error = "An error occurred while hiding the Player's Campaign Map.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<ObserverSight>>.Fail(error);
        }
    }
}
