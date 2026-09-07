using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.ForceOrders.StepForce;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request to step a Force onto one of the eight tiles next to it on the campaign map.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a <c>ForceOrder</c>. An order is a standing instruction carried across turns and worked through a step at a time; this is a
/// single immediate move the player asked for and can see the result of, so it resolves here and now. <c>MoveForceRequest</c> remains the seat of
/// the multi-turn pathfound order.
/// </para>
/// <para>The direction is resolved against the Force's current tile, so the caller never names a destination and cannot move it somewhere distant.</para>
/// </remarks>
public record StepForceRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId,
    MoveDirection Direction
) : IRequest<ServiceResult<StepForceResult>>;

internal class StepForceRequestValidator : Validator<StepForceRequest>
{
    public StepForceRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");

        RuleFor(x => x.Direction)
            .IsInEnum().WithMessage("Direction must be one of the eight compass directions.");
    }
}

internal class StepForceHandler(
    ILogger<StepForceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMediator mediator,
    ICampaignTurnService campaignTurnService,
    IEnumerable<IMovementDomainStrategy> movementDomainStrategies
) : IRequestHandler<StepForceRequest, ServiceResult<StepForceResult>>
{
    public async Task<ServiceResult<StepForceResult>> Handle(StepForceRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new StepForceRequestValidator();
            var validationResult = validator.Validate(request);

            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(StepForceRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<StepForceResult>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Tracked, not AsNoTracking: this is the row the step writes back to. The current tile comes with it, because its coordinates are what the direction is applied to.
            UnitInstance? force = await context.UnitInstances
                .Include(f => f.CampaignMap)
                .FirstOrDefaultAsync(f => f.Id == request.UnitInstanceId && f.GameId == request.GameId, cancelToken);

            ServiceResult<StepForceResult>? forceRefusal = ValidateForce(request, force);
            if (forceRefusal is not null)
            {
                return forceRefusal;
            }

            // ValidateForce has already established that the Force exists; the null-forgiving operator only tells the compiler so.
            ForceMovementProfile? movementProfile = await GetMovementProfileAsync(context, force!.UnitTemplateId, cancelToken);

            if (movementProfile is null)
            {
                // A Force cannot be spawned from an uncompiled template, so reaching here means the snapshot was removed out from under it.
                string error = $"{DescribeForce(force)} has no compiled template, so its movement domain and allowance are unknown.";
                logger.LogError("Unit Instance Id {UnitInstanceId} has no compiled unit template.", force.Id);
                return ServiceResult<StepForceResult>.Fail(error);
            }

            ServiceResult<StepForceResult>? turnRefusal = await ValidateItIsThisPlayersTurnAsync(request, cancelToken);
            if (turnRefusal is not null)
            {
                return turnRefusal;
            }

            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (gameInfoResult.HasSucceeded == false || gameInfoResult.SuccessValue is not { Count: > 0 })
            {
                const string error = "Error getting game info while trying to step a Force.";
                logger.LogWarning(error);
                return ServiceResult<StepForceResult>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame gameInfo = gameInfoResult.SuccessValue!.First();

            var origin = new Coord(force.CampaignMap.XCoord, force.CampaignMap.YCoord);

            // The map may wrap, in which case stepping off one edge arrives at the other. Where it does not, there is simply no tile that way.
            if (!MapDirections.TryGetNeighbour(
                    origin,
                    request.Direction,
                    gameInfo.MapWidth,
                    gameInfo.MapHeight,
                    gameInfo.HasHorizontalWrapping,
                    gameInfo.HasVerticalWrapping,
                    out Coord? destination))
            {
                string error = $"There is no tile {request.Direction} of {origin} — that is the edge of the map.";
                logger.LogInformation("{Error} Unit Instance Id {UnitInstanceId}.", error, request.UnitInstanceId);
                return ServiceResult<StepForceResult>.Fail(error);
            }

            CampaignMap? targetTile = await context.CampaignMaps
                .AsNoTracking()
                .Include(tile => tile.Biome)
                .Include(tile => tile.Geography)
                .FirstOrDefaultAsync(tile => tile.GameId == request.GameId
                        && tile.XCoord == destination!.X
                        && tile.YCoord == destination!.Y,
                    cancelToken);

            if (targetTile is null)
            {
                // The coordinate is inside the map's declared bounds, so a missing row is a hole in the generated map rather than a player mistake.
                string error = $"Coordinate {destination} is not on the campaign map for Game Id {request.GameId}.";
                logger.LogWarning("{Error}", error);
                return ServiceResult<StepForceResult>.NotFound(error);
            }

            var target = new MovementTargetTile
            {
                XCoord = targetTile.XCoord,
                YCoord = targetTile.YCoord,
                GeographyType = targetTile.CampaignMapGeographyTypeId,
                GeographyName = targetTile.Geography.Name,
                GeographyMovementCost = targetTile.Geography.MovementCost,
                BiomeName = targetTile.Biome.Name,
                BiomeMovementCost = targetTile.Biome.MovementCost,
                BiomeIsWater = targetTile.Biome.IsWater,
                BiomeIsTraversable = targetTile.Biome.IsTraversable
            };

            ServiceResult<StepForceResult>? terrainRefusal = ValidateTerrain(force, movementProfile, target);
            if (terrainRefusal is not null)
            {
                return terrainRefusal;
            }

            // Rule 2 of docs/design/units/unit-mechanics.md: half the tile's cost is what it takes to *enter*, but the whole cost is what it *spends*.
            // A Force can therefore finish a step in debt and skip a turn paying it off, which is exactly what the worked scenarios in that document
            // describe. Both values stay in the raw Scale 1,000 representation, so the halving is exact for every authored cost.
            long entryCost = ScaledAmount.FromDecimal(target.MovementCost).RawValue;
            long requiredToEnter = entryCost / 2;

            if (force.CampaignMovementRemaining < requiredToEnter)
            {
                string error = $"{DescribeForce(force)} needs {ScaledAmount.FromRaw(requiredToEnter)} movement points to enter {target.Describe()}, "
                    + $"and has {ScaledAmount.FromRaw(force.CampaignMovementRemaining)}.";
                logger.LogInformation("{Error}", error);
                return ServiceResult<StepForceResult>.Fail(error);
            }

            force.CampaignMapTileId = targetTile.CampaignMapId;
            force.CampaignMovementRemaining -= entryCost;

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation(
                "Unit Instance Id {UnitInstanceId} stepped {Direction} from ({OriginX}, {OriginY}) to ({TargetX}, {TargetY}) for {Cost} movement points, leaving {Remaining}.",
                force.Id,
                request.Direction,
                origin.X,
                origin.Y,
                target.XCoord,
                target.YCoord,
                ScaledAmount.FromRaw(entryCost),
                ScaledAmount.FromRaw(force.CampaignMovementRemaining));

            IReadOnlyCollection<ObserverSight> observers = await GetRefreshedVisionAsync(request, cancelToken);

            return ServiceResult<StepForceResult>.Succeed(new StepForceResult
            {
                UnitInstanceId = force.Id,
                XCoord = target.XCoord,
                YCoord = target.YCoord,
                CampaignMovementRemaining = ScaledAmount.FromRaw(force.CampaignMovementRemaining).ToDecimal(),
                CampaignMovementPoints = ScaledAmount.FromRaw(movementProfile.CampaignMovementPoints).ToDecimal(),
                Observers = observers
            });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while trying to step a Force.";

            logger.LogError(ex, error + " GameId: {GameId}, UnitInstanceId: {UnitInstanceId}, Direction: {Direction}.",
                request.GameId,
                request.UnitInstanceId,
                request.Direction);

            return ServiceResult<StepForceResult>.Fail(error);
        }
    }

    /// <summary>
    /// Checks everything about the Force itself that would stop it moving at all, before any terrain is considered.
    /// </summary>
    /// <remarks>
    /// These are the checks <c>MoveForceHandler.ValidateObjects</c> was written for but never wired up. Ordered from the least to the most specific,
    /// so the player is told the most fundamental thing that is wrong rather than an incidental consequence of it.
    /// </remarks>
    /// <returns><see langword="null"/> when the Force is able to move; otherwise the failure to answer with.</returns>
    private ServiceResult<StepForceResult>? ValidateForce(StepForceRequest request, UnitInstance? force)
    {
        if (force is null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} was not found for Game Id {request.GameId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found for Game Id {GameId}.", request.UnitInstanceId, request.GameId);
            return ServiceResult<StepForceResult>.NotFound(error);
        }

        if (force.PlayerId != request.PlayerId)
        {
            // Forbidden rather than a plain failure: the Force exists and the caller simply has no business ordering it about.
            string error = $"Unit Instance Id {request.UnitInstanceId} does not belong to Player Id {request.PlayerId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} does not belong to Player Id {PlayerId}.", request.UnitInstanceId, request.PlayerId);
            return ServiceResult<StepForceResult>.Forbidden(error);
        }

        if (force.HealthyCount + force.InjuredCount <= 0)
        {
            // TODO: If this happens, the Force and any orders it holds should be auto-deleted rather than left standing on the map.
            string error = $"{DescribeForce(force)} has no troops left and cannot move.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} has no remaining troops and cannot move.", force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        if (force.BattleMapTileId is not null)
        {
            string error = $"{DescribeForce(force)} is in battle and cannot move on the campaign map.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} is currently in battle and cannot take a campaign step.", force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        // Remaining movement is raw fixed-point, but "none left" is zero at any scale. Checked separately from the per-tile threshold below so a Force
        // that is spent — or in debt from an expensive crossing — is told so plainly, rather than being quoted arithmetic about a tile it was never
        // going to reach.
        if (force.CampaignMovementRemaining <= 0)
        {
            string error = $"{DescribeForce(force)} has no movement points left this turn.";
            logger.LogInformation("Unit Instance Id {UnitInstanceId} has no remaining movement points.", force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        return null;
    }

    /// <summary>
    /// Refuses the step when it is another player's turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Game setup builds the campaign turn queue but leaves every participant <c>Pending</c> — nothing promotes the first player to
    /// <c>TakingTurn</c> until someone ends a turn. So a game that has never had a turn ended has no current turn at all, and enforcing this
    /// strictly would make every Force in a new game immovable.
    /// </para>
    /// <para>
    /// The rule is therefore applied wherever the data can answer it and skipped, loudly, where it cannot. Once the turn queue is started at setup
    /// (TM-3 territory) the "no current turn" branch becomes dead and should be replaced with a refusal.
    /// </para>
    /// </remarks>
    /// <returns><see langword="null"/> when the step may proceed; otherwise the failure to answer with.</returns>
    private async Task<ServiceResult<StepForceResult>?> ValidateItIsThisPlayersTurnAsync(StepForceRequest request, CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            logger.LogWarning(
                "No current campaign turn for Game Id {GameId}, so the turn check was skipped for the step by Player Id {PlayerId}. "
                + "The turn queue is not started until a turn is ended.",
                request.GameId,
                request.PlayerId);

            return null;
        }

        if (turnResult.SuccessValue.PlayerId != request.PlayerId)
        {
            string error = $"It is {turnResult.SuccessValue.FactionName}'s turn, so your forces cannot move yet.";
            logger.LogInformation(
                "Player Id {PlayerId} tried to step a Force during Player Id {CurrentPlayerId}'s turn in Game Id {GameId}.",
                request.PlayerId,
                turnResult.SuccessValue.PlayerId,
                request.GameId);

            return ServiceResult<StepForceResult>.Forbidden(error);
        }

        return null;
    }

    /// <summary>
    /// Checks that the target tile can be entered at all, and that this Force's movement domain permits it.
    /// </summary>
    /// <returns><see langword="null"/> when the Force may enter the tile; otherwise the failure to answer with.</returns>
    private ServiceResult<StepForceResult>? ValidateTerrain(UnitInstance force, ForceMovementProfile movementProfile, MovementTargetTile target)
    {
        // Impassability holds for every domain alike, so it is settled before any strategy is consulted.
        if (target.IsImpassable)
        {
            // The two causes read differently to a player, so a mountain is named as one; anything else keeps the general wording.
            string error = target.GeographyIsImpassable ? "Mountain terrain cannot be entered." : "This terrain cannot be entered.";

            // The message no longer names the tile, so the log carries the description instead.
            logger.LogInformation("{Error} {Tile}. Unit Instance Id {UnitInstanceId}.", error, target.Describe(), force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        IMovementDomainStrategy? strategy = movementDomainStrategies.SingleOrDefault(s => s.MovementDomain == movementProfile.MovementDomain);

        if (strategy is null)
        {
            // Every DomainType has a registered strategy, so this is a wiring fault rather than anything the player did.
            string error = $"No movement rules are registered for the {movementProfile.MovementDomain} domain.";
            logger.LogError("{Error} Unit Instance Id {UnitInstanceId}.", error, force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        string? refusal = strategy.GetEntryRefusal(target);

        if (refusal is not null)
        {
            logger.LogInformation("Unit Instance Id {UnitInstanceId} refused entry to {Tile}: {Refusal}", force.Id, target.Describe(), refusal);
            return ServiceResult<StepForceResult>.Fail(refusal);
        }

        return null;
    }

    /// <summary>
    /// Re-runs the fog-of-war read so the step's response carries what the player can now see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the same read the map endpoint serves, which is the point: it already records newly seen tiles against the player's own map and
    /// returns the observed tiles with whatever is standing on them, so the step needs no vision code of its own and the two paths cannot drift.
    /// </para>
    /// <para>
    /// It covers every observer the player owns rather than only the Force that moved, because a step can also uncover ground that a neighbouring
    /// Force was already looking towards. It runs after the save, so the tile the Force left comes back showing it gone.
    /// </para>
    /// <para>
    /// A failure here is logged and swallowed: the step itself has already been committed, so answering with an error would tell the player their
    /// move failed when it did not. They lose the terrain update until the next map read, and nothing worse.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyCollection<ObserverSight>> GetRefreshedVisionAsync(StepForceRequest request, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<ObserverSight>> visionResult = await mediator.Send(
            new GetPlayerCampaignMapRequest(request.GameId, request.PlayerId, LoadWholeMap: false),
            cancelToken);

        if (visionResult.HasSucceeded == false || visionResult.SuccessValue is null)
        {
            logger.LogWarning(
                "The Force step for Unit Instance Id {UnitInstanceId} was applied, but refreshing the Player's view of the map failed: {Errors}",
                request.UnitInstanceId,
                string.Join("; ", visionResult.Errors));

            return [];
        }

        return visionResult.SuccessValue;
    }

    /// <summary>
    /// Reads the two facts about a Force's template that movement depends on: which terrain it can cross, and how far it can go in a turn.
    /// </summary>
    /// <remarks>
    /// Projected rather than loaded as an entity, and queried on its own rather than included alongside the Force. <see cref="CompiledUnitTemplate"/>
    /// owns EF complex types for its damage and resistance profiles, and materialising the entity drags those into the query shape — which is both
    /// wasted work here and, as <c>docs/testing.md</c> records, something the in-memory provider cannot do at all. Selecting only these two scalars
    /// keeps the read cheap and keeps this handler testable without a container.
    /// </remarks>
    private static async Task<ForceMovementProfile?> GetMovementProfileAsync(AscendantEmpireContext context, int unitTemplateId, CancellationToken cancelToken)
    {
        return await context.CompiledUnitTemplates
            .AsNoTracking()
            .Where(compiled => compiled.UnitTemplateId == unitTemplateId)
            .Select(compiled => new ForceMovementProfile(compiled.MovementDomain, compiled.CampaignMovementPoints))
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// Names a Force for a player-facing message, preferring the name it was given over its bare id.
    /// </summary>
    private static string DescribeForce(UnitInstance force)
    {
        return string.IsNullOrWhiteSpace(force.Name) ? $"Force #{force.Id}" : force.Name;
    }

    /// <summary>
    /// What a Force's compiled template says about how it moves.
    /// </summary>
    /// <param name="MovementDomain">Which terrain it can cross, and so which movement strategy judges its steps.</param>
    /// <param name="CampaignMovementPoints">Its full allowance for a turn, raw on the Scale 1,000 convention.</param>
    private sealed record ForceMovementProfile(DomainType MovementDomain, long CampaignMovementPoints);
}
