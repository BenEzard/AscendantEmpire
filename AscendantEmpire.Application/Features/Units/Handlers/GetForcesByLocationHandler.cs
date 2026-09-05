using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Units.GetPlayerForces;
using AscendantEmpire.Domain.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Units.Handlers;

public record GetForcesByLocationRequest(
    int GameId,
    IEnumerable<int> CampaignMapTileIds,
    bool IncludeBattleMapForces = false
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerForce>>>;

internal class GetForcesByLocationRequestValidator : Validator<GetForcesByLocationRequest>
{
    public GetForcesByLocationRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.CampaignMapTileIds)
            .NotNull().WithMessage("At least one Campaign Map Tile Id is required.")
            .Must(ids => ids != null && ids.Any()).WithMessage("At least one Campaign Map Tile Id is required.");

        RuleForEach(x => x.CampaignMapTileIds)
            .GreaterThan(0).WithMessage("Campaign Map Tile Ids must be greater than 0.");

    }
}

internal class GetForcesByLocationHandler(
    ILogger<GetForcesByLocationHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetForcesByLocationRequest, ServiceResult<IReadOnlyCollection<PlayerForce>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerForce>>> Handle(GetForcesByLocationRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetForcesByLocationRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetForcesByLocationRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerForce>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerForce> Forces = await context.UnitInstances
                  .Where(pu => pu.GameId == request.GameId)
                  .Where(pu => pu.HealthyCount + pu.InjuredCount > 0) // Only include Forces that are alive
                  .Where(pu => request.CampaignMapTileIds.Contains(pu.CampaignMapTileId))
                  .Where(pu => request.IncludeBattleMapForces || pu.BattleMapTileId == null)
                  .Select(pu => new PlayerForce
                  {
                      UnitInstanceId = pu.Id,
                      RecordType = RecordType.Unit,
                      PlayerId = pu.PlayerId,
                      CampaignMapTileId = pu.CampaignMapTileId,
                      BattleMapTileId = pu.BattleMapTileId,
                      CampaignXCoord = pu.CampaignMap.XCoord,
                      CampaignYCoord = pu.CampaignMap.YCoord,
                      SightRange = (int)(pu.UnitTemplate.Compiled!.TotalSight / ScaledAmount.Scale),
                      MapImagePath = pu.UnitTemplate.Compiled!.MapImagePath,
                      HealthyTroops = pu.HealthyCount,
                      InjuredTroops = pu.InjuredCount,
                      InitialStrength = pu.InitialStrength,
                      UnitTemplateId = pu.UnitTemplateId,
                      HasCampaignMovementRemaining = pu.CampaignMovementRemaining > 0,
                      CampaignMovementRemaining = pu.CampaignMovementRemaining / (decimal)ScaledAmount.Scale,
                      CampaignMovementPoints = pu.UnitTemplate.Compiled!.CampaignMovementPoints / (decimal)ScaledAmount.Scale,
                      BattleMovementRemaining = pu.BattleMovementRemaining / (decimal)ScaledAmount.Scale,
                  })
                  .ToListAsync(cancelToken);

            logger.LogInformation("Retrieved {Count} Forces for Game Id: {GameId} and Campaign Map Tile Ids: {CampaignMapTileIds}.", Forces.Count, request.GameId, string.Join(", ", request.CampaignMapTileIds));

            return ServiceResult<IReadOnlyCollection<PlayerForce>>.Succeed(Forces);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while retrieving Forces for Game Id: {request.GameId} and Campaign Map Tile Ids: {string.Join(", ", request.CampaignMapTileIds)}.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<PlayerForce>>.Fail(error);
        }
    }
}
