using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.ForceOrders.StepForce;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
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
    IEnumerable<IMovementDomainStrategy> movementDomainStrategies,
    IForceLifecycleWriter forceLifecycleWriter,
    IBattleEngagementService battleEngagementService
) : IRequestHandler<StepForceRequest, ServiceResult<StepForceResult>>
{
    /// <summary>
    /// The most Turn-ends a single step is allowed to cost a Force before the step is refused outright.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Movement debt is uncapped, so without a bound one step onto a sufficiently expensive tile could park a Force for an arbitrary stretch of the
    /// game. This is the honest form of the gate the old 50% threshold was reaching for: a limit expressed in Turns, which is the cost the player
    /// actually pays, rather than as a proportion of the tile that a slow Force could never reach whatever it did.
    /// </para>
    /// <para>
    /// It refuses nothing under the authored terrain — the most expensive tile on the map is Hilly Jungle at 4, which holds even a 1 point Force for
    /// 4 Turns — and exists to bound a future high-cost biome rather than to shape play today.
    /// </para>
    /// </remarks>
    private const int _MAX_STRANDED_TURNS = 6;

    public async Task<ServiceResult<StepForceResult>> Handle(StepForceRequest request, CancellationToken cancelToken)
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

        // The Turn id comes back with the verdict, because the step is recorded against it. The Round comes too, because a step that starts a Battle records the Round it began in.
        (int playerTurnId, int roundNumber, ServiceResult<StepForceResult>? turnRefusal) = await ValidateItIsThisPlayersTurnAsync(request, cancelToken);
        if (turnRefusal is not null)
        {
            return turnRefusal;
        }

        ServiceResult<IReadOnlyCollection<GameContext>> gameInfoResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancelToken);

        if (gameInfoResult.HasSucceeded == false || gameInfoResult.SuccessValue is not { Count: > 0 })
        {
            const string error = "Error getting game info while trying to step a Force.";
            logger.LogWarning(error);
            return ServiceResult<StepForceResult>.Fail(gameInfoResult.Errors.Prepend(error));
        }

        GameContext gameInfo = gameInfoResult.SuccessValue!.First();

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
            GeographyIsWater = targetTile.Geography.IsWater,
            BiomeIsTraversable = targetTile.Biome.IsTraversable,
            GeographyIsTraversable = targetTile.Geography.IsTraversable
        };

        // The rivers around the Force decide what the step crosses and follows, and the tile it is leaving decides whether a ship is putting out from a
        // river or sailing on open water. Everything either needs lies in the 3x3 block around the origin.
        (RiverEdges riverEdges, bool originIsWater) = await GetStepSurroundingsAsync(context, request.GameId, origin, gameInfo, cancelToken);

        (int deltaX, int deltaY) = MapDirections.GetDelta(request.Direction);

        var step = new MovementStep
        {
            Target = target,
            OriginIsWater = originIsWater,
            IsDiagonal = deltaX != 0 && deltaY != 0,
            Rivers = RiverMovementRules.Evaluate(riverEdges, request.Direction)
        };

        ServiceResult<StepForceResult>? terrainRefusal = ValidateStep(force, movementProfile, step);
        if (terrainRefusal is not null)
        {
            return terrainRefusal;
        }

        // What the step costs this Force: the tile's cost, adjusted for any river crossed or followed as its domain sees it. The stranding check and
        // the payment below both use this one figure, so the guard can never be weighing a different price from the one charged.
        decimal stepCost = MovementStepRules.GetStepCost(movementDomainStrategies, movementProfile.MovementDomain, step);

        // The last of the refusals, and the only one that weighs what the Force is still holding against the step standing in front of it.
        ServiceResult<StepForceResult>? strandingRefusal = ValidateItWouldNotBeStranded(force, movementProfile, target, stepCost);
        if (strandingRefusal is not null)
        {
            return strandingRefusal;
        }

        // Who is already standing on the tile decides whether this is a move or an attack. Both reads come before anything is staged, so refusing here leaves nothing to undo.
        ServiceResult<StepForceResult>? battleRefusal = await ValidateNoBattleIsUnderWayAsync(context, request, force, targetTile.CampaignMapId, target, cancelToken);
        if (battleRefusal is not null)
        {
            return battleRefusal;
        }

        // Every other Player's Force on the tile. Any at all means the step is an attack and starts a Battle; none means it is an ordinary move. Tracked, because each of them is placed on
        // the battle map if it comes to a fight.
        List<UnitInstance> defenders = await GetEnemyForcesOnTileAsync(context, request, targetTile.CampaignMapId, cancelToken);

        // The entry gate in docs/design/units/unit-movement.md: entry is gated on having movement left at all, not on holding some proportion of the
        // tile's cost — ValidateForce settled that already, so no affordability check is left to make here. What remains is the payment: the
        // whole cost comes off and the balance is allowed to go negative, so a Force dives into expensive ground and waits the debt out rather
        // than the ground being permanently out of its reach.
        long entryCost = ScaledAmount.FromDecimal(stepCost).RawValue;

        // The position goes through the lifecycle writer rather than onto the Force directly - the property is init-only, so nothing else can set it. The writer stages the new tile and the
        // Moved row together, so the save below commits both or neither. When it refuses, nothing has been staged, and the step is refused rather than made without a record.
        ServiceResultBase moveResult = await forceLifecycleWriter.MoveAsync(context, force, targetTile.CampaignMapId, playerTurnId, cancelToken);

        if (moveResult.HasSucceeded == false)
        {
            string error = $"{DescribeForce(force)} could not be moved, because the step could not be recorded.";
            logger.LogWarning("{Error} Unit Instance Id {UnitInstanceId}. Errors: {Errors}", error, force.Id, string.Join("; ", moveResult.Errors));
            return ServiceResult<StepForceResult>.Fail(moveResult.Errors.Prepend(error));
        }

        force.CampaignMovementRemaining -= entryCost;

        // An attack pays for the step and moves onto the tile exactly as an ordinary step does; what differs is how it is saved.
        bool startedBattle = defenders.Count > 0;

        if (startedBattle)
        {
            // The engagement service saves, rather than this handler: the move and movement payment staged above are flushed inside the same transaction as the Battle, each Force's
            // starting battle tile and the history rows, so the Force is never standing on the tile without the Battle having begun, nor the Battle begun without it. On failure nothing
            // has been committed, and the context is thrown away with this handler.
            ServiceResult<int> battleResult = await battleEngagementService.CommenceAsync(
                context,
                new BattleEngagement
                {
                    GameId = request.GameId,
                    Attacker = force,
                    Defenders = defenders,
                    CampaignMapTileId = targetTile.CampaignMapId,
                    StepDirection = request.Direction,
                    RoundNumber = roundNumber,
                    PlayerTurnId = playerTurnId
                },
                cancelToken);

            if (battleResult.HasSucceeded == false)
            {
                string error = $"{DescribeForce(force)} could not attack {target.Describe()}.";
                logger.LogWarning("{Error} Unit Instance Id {UnitInstanceId}. Errors: {Errors}", error, force.Id, string.Join("; ", battleResult.Errors));
                return ServiceResult<StepForceResult>.Fail(battleResult.Errors.Prepend(error));
            }

            logger.LogInformation(
                "Unit Instance Id {UnitInstanceId} attacked {DefenderCount} enemy Force(s) on ({TargetX}, {TargetY}), starting Battle Id {BattleId}.",
                force.Id,
                defenders.Count,
                target.XCoord,
                target.YCoord,
                battleResult.SuccessValue);
        }
        else
        {
            await context.SaveChangesAsync(cancelToken);
        }

        logger.LogInformation(
            "Unit Instance Id {UnitInstanceId} stepped {Direction} from ({OriginX}, {OriginY}) to ({TargetX}, {TargetY}) for {Cost} movement points, leaving {Remaining}. "
            + "River crossed: {RiverCrossed}; river along: {RiverAlong}.",
            force.Id,
            request.Direction,
            origin.X,
            origin.Y,
            target.XCoord,
            target.YCoord,
            ScaledAmount.FromRaw(entryCost),
            ScaledAmount.FromRaw(force.CampaignMovementRemaining),
            step.Rivers.Crossed,
            step.Rivers.Along);

        IReadOnlyCollection<ObserverSight> observers = await GetRefreshedVisionAsync(request, cancelToken);

        return ServiceResult<StepForceResult>.Succeed(new StepForceResult
        {
            UnitInstanceId = force.Id,
            XCoord = target.XCoord,
            YCoord = target.YCoord,
            CampaignMovementRemaining = ScaledAmount.FromRaw(force.CampaignMovementRemaining).ToDecimal(),
            CampaignMovementPoints = ScaledAmount.FromRaw(movementProfile.CampaignMovementPoints).ToDecimal(),
            StepCost = stepCost,
            // What the rivers did to the price, as the difference from what the ground alone would have cost. Worked out here rather than asked of the
            // strategy: the strategy already priced the step, and one subtraction says the same thing for every domain without another rule to keep in step.
            RiverMovementCost = stepCost - target.MovementCost,
            StartedBattle = startedBattle,
            RiverCrossed = step.Rivers.Crossed,
            RiverAlong = step.Rivers.Along,
            Observers = observers
        });
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

        // Remaining movement is raw fixed-point, but "none left" is zero at any scale. This is the whole of the entry gate: a Force holding anything
        // at all may enter any tile its domain permits, however expensive, and a Force that is spent — or still in debt from an expensive crossing —
        // may enter nothing. Note that it is deliberately not the tile's cost being tested; that cost is paid, not required up front.
        if (force.CampaignMovementRemaining <= 0)
        {
            string error = $"{DescribeForce(force)} has no movement points left this turn.";
            logger.LogInformation("Unit Instance Id {UnitInstanceId} has no remaining movement points.", force.Id);
            return ServiceResult<StepForceResult>.Fail(error);
        }

        return null;
    }

    /// <summary>
    /// Refuses the step unless it is this player's turn, and hands back the open Turn the step will be recorded against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Game with no current turn at all is refused too. That used to be let through, loudly, because game setup once left every participant <c>Pending</c> until somebody ended a turn. It no
    /// longer does: a Game is marked in progress in the same transaction that opens its first Turn. Reaching here with no current turn is therefore a broken invariant rather than a new Game - and
    /// the step could not be recorded in any case, with no Turn to stamp it with.
    /// </para>
    /// <para>
    /// A current turn with no open <c>PlayerTurn</c> row is refused for the same reason, and logged as an error, since every Turn is opened before its start-of-turn pipeline runs.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The open Turn's id and the current Round, with no refusal, when the step may proceed; otherwise the failure to answer with, alongside an id and Round that mean nothing.
    /// </returns>
    private async Task<(int PlayerTurnId, int RoundNumber, ServiceResult<StepForceResult>? Refusal)> ValidateItIsThisPlayersTurnAsync(StepForceRequest request, CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            const string error = "No turn is in progress in this game, so no forces can move.";
            logger.LogWarning(
                "No current campaign turn for Game Id {GameId}, so the step by Player Id {PlayerId} was refused. Errors: {Errors}",
                request.GameId,
                request.PlayerId,
                string.Join("; ", turnResult.Errors));

            return (0, 0, ServiceResult<StepForceResult>.Fail(error));
        }

        CurrentCampaignTurn currentTurn = turnResult.SuccessValue;

        if (currentTurn.PlayerId != request.PlayerId)
        {
            string error = $"It is {currentTurn.FactionName}'s turn, so your forces cannot move yet.";
            logger.LogInformation(
                "Player Id {PlayerId} tried to step a Force during Player Id {CurrentPlayerId}'s turn in Game Id {GameId}.",
                request.PlayerId,
                currentTurn.PlayerId,
                request.GameId);

            return (0, 0, ServiceResult<StepForceResult>.Forbidden(error));
        }

        if (currentTurn.PlayerTurnId is not int playerTurnId)
        {
            const string error = "Your turn has not been opened properly, so the step cannot be recorded.";
            logger.LogError(
                "Game Id {GameId} has a current campaign turn for Player Id {PlayerId} but no open Player Turn to record a step against.",
                request.GameId,
                request.PlayerId);

            return (0, 0, ServiceResult<StepForceResult>.Fail(error));
        }

        return (playerTurnId, currentTurn.CurrentRoundNumber, null);
    }

    /// <summary>
    /// Refuses a step onto a tile where a Battle is already being fought.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stop-gap. The intent is that the Force joins the Battle instead - reinforcing its own side if its Player is already fighting there, or entering as a new side on the edge matching its
    /// step if not. Until that is built, refusing keeps the tile to the one Battle the unique index on <c>battle.Battle</c> allows, rather than letting a Force walk into the middle of a fight
    /// untouched by it.
    /// </para>
    /// </remarks>
    /// <returns><see langword="null"/> when no Battle is under way on the tile; otherwise the failure to answer with.</returns>
    private async Task<ServiceResult<StepForceResult>?> ValidateNoBattleIsUnderWayAsync(
        AscendantEmpireContext context,
        StepForceRequest request,
        UnitInstance force,
        int targetTileId,
        MovementTargetTile target,
        CancellationToken cancelToken)
    {
        bool battleUnderWay = await context.Battles
            .AsNoTracking()
            .AnyAsync(battle => battle.GameId == request.GameId && battle.CampaignMapTileId == targetTileId && battle.ConcludedAtRoundNumber == null, cancelToken);

        if (!battleUnderWay)
        {
            return null;
        }

        // TODO COMM-4: Join the Battle through IBattleLifecycleWriter.JoinAsync rather than refusing the step. A Player already fighting there reinforces their side; one allied with neither
        // side enters as a new one, placed on the battle map edge matching their step direction.
        string error = $"A battle is already under way on {target.Describe()}, so {DescribeForce(force)} cannot enter it yet.";
        logger.LogInformation("Unit Instance Id {UnitInstanceId} refused entry to {Tile}: a Battle is already under way there.", force.Id, target.Describe());

        return ServiceResult<StepForceResult>.Fail(error);
    }

    /// <summary>
    /// Finds every other Player's Force standing on the tile, which the stepping Force would be attacking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Any other Player counts as an enemy, since there is no alliance model yet, and every Force is assumed to be seen - detection, ambush and hiding are not modelled. Only Forces with troops
    /// left are counted, because a wiped-out Force is not something to fight.
    /// </para>
    /// <para>
    /// A Force already on a battle map is left out. With no open Battle on the tile (which <see cref="ValidateNoBattleIsUnderWayAsync"/> has already established) there should be none, and a
    /// Force that somehow is must not be placed a second time.
    /// </para>
    /// <para>
    /// Tracked, and ordered by id: tracked because each one's starting battle tile is written onto it if the step goes ahead, and ordered so the Battle that results is the same however the
    /// database happened to return the rows.
    /// </para>
    /// </remarks>
    private static async Task<List<UnitInstance>> GetEnemyForcesOnTileAsync(AscendantEmpireContext context, StepForceRequest request, int targetTileId, CancellationToken cancelToken)
    {
        return await context.UnitInstances
            .Where(other => other.GameId == request.GameId
                && other.CampaignMapTileId == targetTileId
                && other.PlayerId != request.PlayerId
                && other.HealthyCount + other.InjuredCount > 0
                && other.BattleMapTileId == null)
            .OrderBy(other => other.Id)
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Checks that the target tile can be entered at all, and that this Force's movement domain permits this step onto it, rivers included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The decision itself belongs to <see cref="MovementStepRules"/>. It agrees with <see cref="MovementEntryRules"/>, which the fog-of-war read asks, on
    /// everything about the tile, and differs only where the step's shape matters: a Land Force refused a Large river, or a ship sailing up one onto land.
    /// Only the wording and logging are this handler's.
    /// </para>
    /// </remarks>
    /// <returns><see langword="null"/> when the Force may take the step; otherwise the failure to answer with.</returns>
    private ServiceResult<StepForceResult>? ValidateStep(UnitInstance force, ForceMovementProfile movementProfile, MovementStep step)
    {
        MovementTargetTile target = step.Target;
        MovementEntryRefusal? refusal = MovementStepRules.GetStepRefusal(movementDomainStrategies, movementProfile.MovementDomain, step);

        if (refusal is null)
        {
            return null;
        }

        if (refusal.IsRuleFault)
        {
            logger.LogError("{Error} Unit Instance Id {UnitInstanceId}.", refusal.Reason, force.Id);
        }
        else
        {
            // The message never names the tile, so the log carries the description instead.
            logger.LogInformation("Unit Instance Id {UnitInstanceId} refused entry to {Tile}: {Refusal}", force.Id, target.Describe(), refusal.Reason);
        }

        return ServiceResult<StepForceResult>.Fail(refusal.Reason);
    }

    /// <summary>
    /// Refuses a step that would leave the Force standing on the target tile for longer than <see cref="_MAX_STRANDED_TURNS"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept here, alongside the movement points it depends on, and deliberately <b>not</b> folded into <see cref="MovementEntryRules"/>. That class
    /// is shared with the fog-of-war read, which asks it what a blinded Observer could never have walked onto; a rule that varied with how much
    /// movement a Force happened to be holding would make revealed terrain appear and disappear from the map as the Force tired.
    /// </para>
    /// <para>The refusal quotes the count rather than the arithmetic behind it, because the count is the thing the player is being asked to weigh.</para>
    /// </remarks>
    /// <param name="stepCost">What the step costs this Force, rivers included — the same figure it is charged.</param>
    /// <returns><see langword="null"/> when the step is within the limit; otherwise the failure to answer with.</returns>
    private ServiceResult<StepForceResult>? ValidateItWouldNotBeStranded(UnitInstance force, ForceMovementProfile movementProfile, MovementTargetTile target, decimal stepCost)
    {
        int turnsHeld = MovementTargetTile.TurnsHeld(stepCost, movementProfile.CampaignMovementPoints, force.CampaignMovementRemaining);

        if (turnsHeld <= _MAX_STRANDED_TURNS)
        {
            return null;
        }

        string error = $"{DescribeForce(force)} would be held on {target.Describe()} for {turnsHeld} turns, and no step may cost more than "
            + $"{_MAX_STRANDED_TURNS}.";

        logger.LogInformation(
            "Unit Instance Id {UnitInstanceId} refused entry to {Tile}: it would be held there for {TurnsHeld} turns on an allowance of {Allowance} "
            + "with {Remaining} remaining.",
            force.Id,
            target.Describe(),
            turnsHeld,
            ScaledAmount.FromRaw(movementProfile.CampaignMovementPoints),
            ScaledAmount.FromRaw(force.CampaignMovementRemaining));

        return ServiceResult<StepForceResult>.Fail(error);
    }

    /// <summary>
    /// Reads what a step needs to know about the ground around the Force: the river edges of the 3x3 block around it, and whether the tile it is leaving
    /// is water.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One projected, untracked query. The block is fetched as every combination of its (up to) three columns and three rows, which is exactly the block
    /// even where the map wraps, because the wrapped coordinates come from <see cref="MapDirections.GetNeighbours"/>.
    /// </para>
    /// <para>
    /// The origin's water-ness goes through <see cref="CampaignTileTerrain"/>, the same rule the target tile uses, so a ship putting back out from a river
    /// and a ship on open sea are told apart by the rule everything else uses.
    /// </para>
    /// </remarks>
    private static async Task<(RiverEdges RiverEdges, bool OriginIsWater)> GetStepSurroundingsAsync(
        AscendantEmpireContext context,
        int gameId,
        Coord origin,
        GameContext gameInfo,
        CancellationToken cancelToken)
    {
        IReadOnlyCollection<Coord> neighbours = MapDirections.GetNeighbours(
            origin,
            gameInfo.MapWidth,
            gameInfo.MapHeight,
            gameInfo.HasHorizontalWrapping,
            gameInfo.HasVerticalWrapping);

        List<int> columns = [.. neighbours.Select(coord => coord.X).Append(origin.X).Distinct()];
        List<int> rows = [.. neighbours.Select(coord => coord.Y).Append(origin.Y).Distinct()];

        var tiles = await context.CampaignMaps
            .AsNoTracking()
            .Where(tile => tile.GameId == gameId && columns.Contains(tile.XCoord) && rows.Contains(tile.YCoord))
            .Select(tile => new
            {
                tile.XCoord,
                tile.YCoord,
                tile.SouthRiver,
                tile.EastRiver,
                BiomeIsWater = tile.Biome.IsWater,
                GeographyIsWater = tile.Geography.IsWater
            })
            .ToListAsync(cancelToken);

        Dictionary<Coord, TileRiverEdges> edgesByCoord = tiles.ToDictionary(
            tile => new Coord(tile.XCoord, tile.YCoord),
            tile => new TileRiverEdges(tile.SouthRiver, tile.EastRiver));

        var originTile = tiles.SingleOrDefault(tile => tile.XCoord == origin.X && tile.YCoord == origin.Y);

        // The Force is standing on the origin, so it has a row. Were it somehow missing, treating it as land is the conservative reading: a ship is then
        // held to the stricter river rules rather than being let out onto land.
        bool originIsWater = originTile is not null && CampaignTileTerrain.IsWater(originTile.BiomeIsWater, originTile.GeographyIsWater);

        RiverEdges riverEdges = RiverEdges.Create(
            origin,
            edgesByCoord,
            gameInfo.MapWidth,
            gameInfo.MapHeight,
            gameInfo.HasHorizontalWrapping,
            gameInfo.HasVerticalWrapping);

        return (riverEdges, originIsWater);
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
