using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Players;

public record GetPlayerStructuresRequest(
    Guid GameGuid,
    Guid PlayerGuid
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerStructure>>>;

internal class GetPlayerStructuresRequestValidator : AbstractValidator<GetPlayerStructuresRequest>
{
    public GetPlayerStructuresRequestValidator()
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

internal class GetPlayerStructuresHandler(
    ILogger<GetPlayerStructuresHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetPlayerStructuresRequest, ServiceResult<IReadOnlyCollection<PlayerStructure>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerStructure>>> Handle(GetPlayerStructuresRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetPlayerStructuresRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetPlayerStructuresRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Fail(errors.Prepend(error));
            }

            ServiceResult<IReadOnlyCollection<OpenGame>> openGameResult = await mediator.Send(new GetOpenGamesRequest(request.GameGuid, request.PlayerGuid), cancelToken);

            if (openGameResult.SuccessValue is not { Count: > 0 } openGames)
            {
                string error = $"No open game found for GameGuid: {request.GameGuid} and PlayerGuid: {request.PlayerGuid}.";
                logger.LogWarning(error);
                return ServiceResult<IReadOnlyCollection<PlayerStructure>>.NotFound(error);
            }

            int gameId = openGames.First().GameId;
            int playerId = openGames.First().Players.First().PlayerId;

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerStructure> structures = await (from si in context.StructureInstances.AsNoTracking()
                              join tile in context.CampaignMaps.AsNoTracking()
                                on si.CampaignMapTileId equals tile.CampaignMapId
                              where si.GameId == gameId
                                && si.OwnedByPlayerId == playerId
                                && si.BattleMapTileId == null // campaign only
                                && si.RemovedOnTurnId == null // active only
                              select new PlayerStructure
                              {
                                  CampaignMapTileId = si.CampaignMapTileId,
                                  BattleMapTileId = si.BattleMapTileId,
                                  XCoord = tile.XCoord,
                                  YCoord = tile.YCoord,
                                  StructureId = si.StructureId,
                                  Name = si.Structure.Name,
                                  Code = si.Structure.Code,
                                  CompletionPercent = si.CompletionPercent,
                                  OwnerPlayerId = si.OwnedByPlayerId,
                                  SightRange = si.Structure.SightRange
                              }).ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Succeed(structures);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(GetPlayerStructuresRequest));
            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Fail("An unexpected error occurred while retrieving player structures.");
        }
    }
}
