using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Players.Handlers;

public record GetPlayerStructuresRequest(
    int GameId,
    int PlayerId
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerStructure>>>;

internal class GetPlayerStructuresRequestValidator : Validator<GetPlayerStructuresRequest>
{
    public GetPlayerStructuresRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
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
        // Validate the request object
        var validator = new GetPlayerStructuresRequestValidator();

        if (validator.Validate(request) is not { IsValid: true })
        {
            var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetPlayerStructuresRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Fail(errors.Prepend(error));
        }

        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancelToken);

        if (gameContextResult.SuccessValue is not { Count: > 0 } gameContexts)
        {
            string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
            logger.LogWarning(error);
            return ServiceResult<IReadOnlyCollection<PlayerStructure>>.NotFound(error);
        }

        int gameId = gameContexts.First().GameId;
        int playerId = gameContexts.First().Players.First().PlayerId;

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        List<PlayerStructure> structures = await context.StructureInstances
            .Where(si => si.GameId == gameId
                      && si.OwnedByPlayerId == playerId
                      && si.BattleMapTileId == null // campaign only
                      && si.RemovedOnTurnId == null) // active only
            .Select(si => new PlayerStructure
            {
                StructureInstanceId = si.Id,
                RecordType = Domain.Enums.RecordType.Structure,
                CampaignMapTileId = si.CampaignMapTileId,
                BattleMapTileId = si.BattleMapTileId,
                XCoord = si.CampaignMap.XCoord,
                YCoord = si.CampaignMap.YCoord,
                StructureId = si.StructureId,
                SlotType = si.SlotType,
                Position = si.Position,
                Name = si.Structure.Name,
                Code = si.Structure.Code,
                SettlementName = context.Settlements
                    .Where(settlement => settlement.StructureInstanceId == si.Id)
                    .Select(settlement => settlement.Name)
                    .FirstOrDefault(),
                // Derived on the way out: the Structure carries work done, and the percentage is the UI's way of reading it. A kind that takes no work has nothing to
                // divide by and reads 0 until it is finished, which its CompletedOnTurnId says.
                CompletionPercent = si.Structure.BuildPointsRequired <= 0
                    ? 0d
                    : 100d * si.BuildPointsProgress / si.Structure.BuildPointsRequired,
                OwnerPlayerId = si.OwnedByPlayerId,
                SightRange = si.Structure.SightRange,
                DetectionDirection = si.Structure.SightDirection,
                MapImagePath = si.Structure.MapImagePath
            })
            .ToListAsync(cancelToken);

        return ServiceResult<IReadOnlyCollection<PlayerStructure>>.Succeed(structures);
    }
}
