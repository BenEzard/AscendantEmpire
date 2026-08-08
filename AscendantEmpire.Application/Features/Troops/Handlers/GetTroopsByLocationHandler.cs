using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Units.GetPlayerTroops;
using AscendantEmpire.Domain.Enums;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Troops.Handlers;

public record GetTroopsByLocationRequest(
    int GameId,
    IEnumerable<int> CampaignMapTileIds,
    bool IncludeBattleMapTroops = false
) : IRequest<ServiceResult<IReadOnlyCollection<PlayerTroop>>>;

internal class GetTroopsByLocationRequestValidator : AbstractValidator<GetTroopsByLocationRequest>
{
    public GetTroopsByLocationRequestValidator()
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

internal class GetTroopsByLocationHandler(
    ILogger<GetTroopsByLocationHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTroopsByLocationRequest, ServiceResult<IReadOnlyCollection<PlayerTroop>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<PlayerTroop>>> Handle(GetTroopsByLocationRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new GetTroopsByLocationRequestValidator();
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTroopsByLocationRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            List<PlayerTroop> troops = await context.TroopInstances
                  .Where(pu => pu.GameId == request.GameId)
                  .Where(pu => pu.HealthyCount + pu.InjuredCount > 0) // Only include troops that are alive
                  .Where(pu => request.CampaignMapTileIds.Contains(pu.CampaignMapTileId))
                  .Where(pu => request.IncludeBattleMapTroops || pu.BattleMapTileId == null)
                  .Select(pu => new PlayerTroop
                  {
                      TroopInstanceId = pu.Id,
                      RecordType = RecordType.Unit,
                      PlayerId = pu.PlayerId,
                      CampaignMapTileId = pu.CampaignMapTileId,
                      BattleMapTileId = pu.BattleMapTileId,
                      CampaignXCoord = pu.CampaignMap.XCoord,
                      CampaignYCoord = pu.CampaignMap.YCoord,
                      // Sight range in tiles, derived from the compiled snapshot's aggregated detection
                      // (fixed-point, Scale 1,000 — see the template compilation). Stand-in until a dedicated
                      // sight-range stat is compiled. The snapshot always exists: a troop cannot be spawned
                      // from an uncompiled template.
                      SightRange = (int)(pu.UnitTemplate.Compiled!.TotalDetection / 1000),
                      MapImagePath = pu.UnitTemplate.Compiled!.MapImagePath,
                      HealthyTroops = pu.HealthyCount,
                      InjuredTroops = pu.InjuredCount,
                      InitialStrength = pu.InitialStrength,
                      UnitTemplateId = pu.UnitTemplateId,

                  })
                  .ToListAsync(cancelToken);

            logger.LogInformation("Retrieved {Count} troops for Game Id: {GameId} and Campaign Map Tile Ids: {CampaignMapTileIds}.", troops.Count, request.GameId, string.Join(", ", request.CampaignMapTileIds));

            return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Succeed(troops);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while retrieving troops for Game Id: {request.GameId} and Campaign Map Tile Ids: {string.Join(", ", request.CampaignMapTileIds)}.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<PlayerTroop>>.Fail(error);
        }
    }
}
