using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
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
    int UnitTemplateId,
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

        RuleFor(x => x.UnitTemplateId)
            .GreaterThan(0).WithMessage("Unit Template Id must be greater than 0.");

        RuleFor(x => x.InitialStrength)
            .GreaterThan(0).WithMessage("Initial Strength must be greater than 0.");

        RuleFor(x => x.HealthyCount)
            .GreaterThan(0).WithMessage("Healthy Count must be greater than 0.");

        RuleFor(x => x.InjuredCount)
            .GreaterThanOrEqualTo(0).WithMessage("Injured Count must be greater than or equal to 0.");

        RuleFor(x => x)
            .Must(x => x.HealthyCount + x.InjuredCount <= x.InitialStrength)
            .WithMessage("The sum of Healthy Count and Injured Count must be less than or equal to Initial Strength.");
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

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Resolve the coordinates to this game's actual tile row rather than calculating an id from them.
            // Deriving it would usually agree, but nothing bounds the requested coordinates against the map size, and a coordinate past the edge still produces a perfectly valid id — one
            // belonging to another game's block, which the foreign key would happily accept. Looking the tile up rejects the coordinate instead, and is what GameSetupService already does for start
            // positions. UX_CampaignMap_Game_X_Y covers this lookup.
            CampaignMap? targetTile = await context.CampaignMaps
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.GameId == gameInfo.GameId
                        && cm.XCoord == request.XCoord
                        && cm.YCoord == request.YCoord,
                    cancelToken);

            if (targetTile is null)
            {
                string error = $"Coordinate ({request.XCoord}, {request.YCoord}) is not on the campaign map for Game Id {gameInfo.GameId}.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            // The troop's stats are served from the template's compiled snapshot, so a template that has never
            // been compiled cannot be fielded. The snapshot also carries the organisation tag resolved from the
            // template's components, which decides whether the unled sizing rules apply.
            CompiledUnitTemplate? compiledTemplate = await context.CompiledUnitTemplates
                .AsNoTracking()
                .Include(c => c.OrganisationTag)
                .FirstOrDefaultAsync(c => c.UnitTemplateId == request.UnitTemplateId, cancelToken);

            if (compiledTemplate is null)
            {
                string error = $"Unit template {request.UnitTemplateId} has no compiled snapshot — it does not exist or has never been compiled.";
                logger.LogWarning("{Error}", error);
                return ServiceResultBase.NotFound(error);
            }

            // Unled units (settlers, scouts, ...) must be a whole multiple of their tag's base quantum, capped
            // at the tag's max. Commissioned templates pass through — their tier-size rules are enforced elsewhere.
            List<string> sizeErrors = UnledSpawnRules.ValidateSize(compiledTemplate.OrganisationTag, request.InitialStrength);

            if (sizeErrors.Count > 0)
            {
                logger.LogWarning("Unled size validation failed for {Request}. Errors: {Errors}", nameof(SpawnTroopRequest), string.Join("; ", sizeErrors));
                return ServiceResultBase.Fail(sizeErrors);
            }

            context.UnitInstances.Add(new UnitInstance
            {
                GameId = gameInfo.GameId,
                PlayerId = thisPlayer.PlayerId,
                CampaignMapTileId = targetTile.CampaignMapId,
                UnitTemplateId = request.UnitTemplateId,
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
