using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.ForceOrders.SettleForce;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request to consume a Force and found a settlement where it stood.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a <c>ForceOrder</c>; <i>construction</i> occurs across Turns, and that lives on the Structure, not an order.
/// </para>
/// </remarks>
public record SettleForceRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId,
    SettlementTier Tier,
    string? Name
) : IRequest<ServiceResult<SettleForceResult>>;

internal class SettleForceRequestValidator : Validator<SettleForceRequest>
{
    public SettleForceRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");

        RuleFor(x => x.Tier)
            .IsInEnum().WithMessage("Tier must be a settlement tier.");

        // Only for a tier the player names. A camp is not one, and the handler has already dropped anything sent for it, so there is nothing here to judge.
        //
        // Trimmed before this runs, so a name of nothing but spaces is caught by the first rule rather than passing it and failing the second.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A settlement name is required.")
            .MaximumLength(Settlement.NAME_MAX_LENGTH)
                .WithMessage($"A settlement name can be at most {Settlement.NAME_MAX_LENGTH} characters.")
            .When(x => SettlementTierRules.For(x.Tier)?.IsNamed == true);
    }
}

internal class SettleForceHandler(
    ILogger<SettleForceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTurnService campaignTurnService,
    IForceContextActionService contextActionService,
    IForceLifecycleWriter forceLifecycleWriter,
    IStructureLifecycleWriter structureLifecycleWriter
) : IRequestHandler<SettleForceRequest, ServiceResult<SettleForceResult>>
{
    public async Task<ServiceResult<SettleForceResult>> Handle(SettleForceRequest request, CancellationToken cancelToken)
    {
        // Null for a tier that carries no name, whatever the client sent, so a camp cannot be made to hold one by a request that ignores the rule.
        //
        // Trimmed before validation so the length rule judges what will actually be stored, and so the uniqueness index cannot be sidestepped with a trailing space.
        string? name = SettlementTierRules.For(request.Tier)?.IsNamed == true
            ? request.Name?.Trim() ?? string.Empty
            : null;

        SettleForceRequest trimmedRequest = request with { Name = name };

        var validator = new SettleForceRequestValidator();
        ValidationResult validationResult = validator.Validate(trimmedRequest);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(SettleForceRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<SettleForceResult>.Fail(errors.Prepend(error));
        }

        SettlementTierDefinition? tierDefinition = SettlementTierRules.For(request.Tier);

        if (tierDefinition is null)
        {
            string error = $"A {request.Tier} cannot be founded directly; only {string.Join(" and ", SettlementTierRules.FoundableTiers)} can.";
            logger.LogWarning("Player Id {PlayerId} tried to found a {Tier}, which is not a foundable tier.", request.PlayerId, request.Tier);

            return ServiceResult<SettleForceResult>.Fail(error);
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Tracked: the Force is destroyed through IForceLifecycleWriter, which needs the tracked entity. Its tile comes with it, for the coordinates on the response.
        UnitInstance? force = await context.UnitInstances
            .Include(f => f.CampaignMap)
            .FirstOrDefaultAsync(f => f.Id == request.UnitInstanceId && f.GameId == request.GameId, cancelToken);

        if (force is null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} is not in this game.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found in Game Id {GameId}.", request.UnitInstanceId, request.GameId);

            return ServiceResult<SettleForceResult>.NotFound(error);
        }

        if (force.PlayerId != request.PlayerId)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} does not belong to you.";
            logger.LogWarning("Player Id {PlayerId} tried to settle Unit Instance Id {UnitInstanceId}, which belongs to Player Id {OwnerPlayerId}.",
                request.PlayerId, request.UnitInstanceId, force.PlayerId);

            return ServiceResult<SettleForceResult>.Forbidden(error);
        }

        (int playerTurnId, int roundNumber, ServiceResult<SettleForceResult>? turnRefusal) = await ValidateItIsThisPlayersTurnAsync(request, cancelToken);

        if (turnRefusal is not null)
        {
            return turnRefusal;
        }

        // The rules themselves, re-run.
        ServiceResult<ForceActionAvailability> availabilityResult = await contextActionService.EvaluateActionAsync(
            request.GameId,
            request.PlayerId,
            [request.UnitInstanceId],
            tierDefinition.ActionType,
            cancelToken);

        if (availabilityResult.HasSucceeded == false || availabilityResult.SuccessValue is null)
        {
            const string error = "The settle order could not be judged against the force's current situation.";
            logger.LogWarning("Could not evaluate {ActionType} for Unit Instance Id {UnitInstanceId}: {Errors}",
                tierDefinition.ActionType, request.UnitInstanceId, string.Join("; ", availabilityResult.Errors));

            return ServiceResult<SettleForceResult>.Fail(availabilityResult.Errors.Prepend(error));
        }

        ForceActionAvailability availability = availabilityResult.SuccessValue;

        if (availability.IsEnabled == false)
        {
            // The strategy's own wording, which is already written to be shown to the player as it stands.
            logger.LogInformation("Refused {ActionType} for Unit Instance Id {UnitInstanceId}: {Reasons}",
                tierDefinition.ActionType, request.UnitInstanceId, string.Join("; ", availability.DisabledReasons));

            return ServiceResult<SettleForceResult>.Fail(availability.DisabledReasons);
        }

        Structure? structureKind = await context.Structures
            .FirstOrDefaultAsync(s => s.Code == tierDefinition.StructureCode, cancelToken);

        if (structureKind is null)
        {
            // The catalogue is seeded rather than migrated, so a database nobody has seeded reaches here. That is a configuration fault, not a player mistake.
            string error = $"The '{tierDefinition.StructureCode}' structure is not in the catalogue, so a {request.Tier} cannot be founded. Seed the Structures catalogue.";
            logger.LogError("Structure code '{StructureCode}' is missing from the catalogue, so a {Tier} could not be founded.", tierDefinition.StructureCode, request.Tier);

            return ServiceResult<SettleForceResult>.Fail(error);
        }

        // Case-insensitive to match the unique index.
        bool nameTaken = name is not null
            && await context.Settlements.AnyAsync(s => s.GameId == request.GameId && s.Name == name, cancelToken);

        if (nameTaken)
        {
            string error = $"There is already a settlement called '{name}'.";
            logger.LogInformation("Player Id {PlayerId} tried to found a settlement named '{Name}', which is taken in Game Id {GameId}.",
                request.PlayerId, name, request.GameId);

            return ServiceResult<SettleForceResult>.Fail(error);
        }

        return await FoundAsync(context, request, force, structureKind, playerTurnId, roundNumber, name, cancelToken);
    }

    /// <summary>
    /// Performs the founding itself, once every refusal has been ruled out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One transaction over four changes, because a half-done founding is worse than none: the tile's existing Primary structure razed, the Force's standing orders cleared,
    /// the Force destroyed, the new Structure founded, and the Settlement written.
    /// </para>
    /// <para>
    /// The transaction is opened <b>here</b> rather than left to <c>FoundAsync</c>, which opens its own when the caller has none. Opening it first is what makes the razing
    /// and the destruction part of the same unit of work as the founding, instead of three that can each land without the others.
    /// </para>
    /// </remarks>
    private async Task<ServiceResult<SettleForceResult>> FoundAsync(
        AscendantEmpireContext context,
        SettleForceRequest request,
        UnitInstance force,
        Structure structureKind,
        int playerTurnId,
        int roundNumber,
        string? name,
        CancellationToken cancelToken)
    {
        int tileId = force.CampaignMapTileId;
        int population = force.HealthyCount + force.InjuredCount;

        // How fast this settlement will go up, worked out from the Force that is about to disappear into it. Fixed here because this is the only moment it can be: the
        // settler is consumed before the end-of-Turn feed, so by the time construction advances there is no Force left to ask.
        //
        // Scalars only off the compiled template, which is what keeps the handler's own query off the EF complex types it owns - see docs/testing.md.
        ForceConstructionProfile? constructionProfile = await context.CompiledUnitTemplates
            .AsNoTracking()
            .Where(compiled => compiled.UnitTemplateId == force.UnitTemplateId)
            .Select(compiled => new ForceConstructionProfile(compiled.Power, compiled.CampaignMovementPoints))
            .FirstOrDefaultAsync(cancelToken);

        if (constructionProfile is null)
        {
            // A Force cannot be spawned from an uncompiled template, so reaching here means the snapshot was removed out from under it.
            string error = $"Unit Instance Id {force.Id} has no compiled template, so how fast it would build is unknown.";
            logger.LogError("Unit Instance Id {UnitInstanceId} has no compiled unit template, so a {Tier} could not be founded.", force.Id, request.Tier);

            return ServiceResult<SettleForceResult>.Fail(error);
        }

        long buildPointsPerTurn = ConstructionRateCalculator.CalculateBuildPointsPerTurn(
            bodyPowerRaw: constructionProfile.PowerRaw,
            healthyCount: force.HealthyCount,
            campaignMovementRemainingRaw: force.CampaignMovementRemaining,
            campaignMovementPointsRaw: constructionProfile.CampaignMovementPointsRaw);

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        // A tile holds at most one Primary structure, and the settlement wants that slot. Replacing it silently is the agreed behaviour for now; a confirmation before
        // destroying somebody's watch tower is a follow-up.
        //
        // Razing rather than deleting, so the displaced structure keeps its history and is reported to anyone who watched it go.
        StructureInstance? displaced = await context.StructureInstances
            .FirstOrDefaultAsync(
                s => s.CampaignMapTileId == tileId
                  && s.BattleMapTileId == null
                  && s.SlotType == TileSlotType.Primary
                  && s.RemovedOnTurnId == null,
                cancelToken);

        if (displaced is not null)
        {
            ServiceResultBase razeResult = await structureLifecycleWriter.RazeAsync(context, displaced, playerTurnId, cancelToken);

            if (razeResult.HasSucceeded == false)
            {
                const string error = "The structure already standing on this tile could not be cleared.";
                logger.LogWarning("Could not raze Structure Instance Id {StructureInstanceId} to make room for a settlement: {Errors}",
                    displaced.Id, string.Join("; ", razeResult.Errors));

                return ServiceResult<SettleForceResult>.Fail(razeResult.Errors.Prepend(error));
            }
        }

        await ClearStandingOrdersAsync(context, force.Id, cancelToken);

        ServiceResultBase destroyResult = await forceLifecycleWriter.DestroyAsync(context, force, playerTurnId, cancelToken);

        if (destroyResult.HasSucceeded == false)
        {
            const string error = "The force could not be consumed, so no settlement was founded.";
            logger.LogWarning("Could not destroy Unit Instance Id {UnitInstanceId} while settling: {Errors}", force.Id, string.Join("; ", destroyResult.Errors));

            return ServiceResult<SettleForceResult>.Fail(destroyResult.Errors.Prepend(error));
        }

        var structure = new StructureInstance
        {
            GameId = request.GameId,
            CampaignMapTileId = tileId,
            BattleMapTileId = null,
            StructureId = structureKind.Id,
            // The writer measures the progress below against this kind's total, so it is handed the kind rather than left to load it. Tracked by this context already,
            // so setting the navigation attaches nothing new.
            Structure = structureKind,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = request.PlayerId,
            // Founding starts construction; it does not finish it. The BuildingStructure end-of-Turn process takes it from here, at this rate.
            BuildPointsProgress = 0,
            BuildPointsPerTurn = buildPointsPerTurn
        };

        // Saves, unlike the other writer methods, because the history row needs the new Structure's id. That save flushes the razing and the destruction above with it.
        ServiceResultBase foundResult = await structureLifecycleWriter.FoundAsync(context, structure, playerTurnId, cancelToken);

        if (foundResult.HasSucceeded == false)
        {
            const string error = "The settlement's structure could not be founded.";
            logger.LogWarning("Could not found a structure for a {Tier} on Campaign Map Tile Id {TileId}: {Errors}",
                request.Tier, tileId, string.Join("; ", foundResult.Errors));

            return ServiceResult<SettleForceResult>.Fail(foundResult.Errors.Prepend(error));
        }

        var settlement = new Settlement
        {
            GameId = request.GameId,
            StructureInstanceId = structure.Id,
            FoundingPlayerId = request.PlayerId,
            Name = name,
            Tier = request.Tier,
            Population = population,
            FoundedOnRoundNumber = roundNumber,
            FoundedOnTurnId = playerTurnId
        };

        context.Settlements.Add(settlement);

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);

        logger.LogInformation(
            "Player Id {PlayerId} founded {Tier} '{Name}' (Settlement Id {SettlementId}) on Campaign Map Tile Id {TileId} with {Population} people, consuming Unit Instance Id {UnitInstanceId}.",
            request.PlayerId, request.Tier, name, settlement.Id, tileId, population, force.Id);

        return ServiceResult<SettleForceResult>.Succeed(new SettleForceResult
        {
            SettlementId = settlement.Id,
            StructureInstanceId = structure.Id,
            Name = name,
            Tier = request.Tier,
            Population = population,
            XCoord = force.CampaignMap.XCoord,
            YCoord = force.CampaignMap.YCoord,
            // Always 0 here, and derived rather than restated so it cannot drift from what the Structure actually carries.
            CompletionPercent = structureKind.BuildPointsRequired <= 0
                ? 0
                : 100d * structure.BuildPointsProgress / structureKind.BuildPointsRequired,
            // What the construction process will actually take at this rate, not a figure off the catalogue: the same Structure founded by a smaller Force takes longer.
            TurnsToComplete = ConstructionRateCalculator.CalculateTurnsToComplete(structureKind.BuildPointsRequired, buildPointsPerTurn)
        });
    }

    /// <summary>
    /// What a Force's compiled template says about how fast it builds.
    /// </summary>
    /// <remarks>Both raw Scale-1,000, as <c>ConstructionRateCalculator</c> wants them.</remarks>
    private sealed record ForceConstructionProfile(long PowerRaw, long CampaignMovementPointsRaw);

    /// <summary>
    /// Removes the standing orders that would otherwise refuse the Force's deletion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>units.ForceOrders</c> and <c>units.ForceOrderParticipants</c> both hold a real foreign key to <c>UnitInstance</c> with <c>NoAction</c>, so the database refuses to
    /// delete a Force that any of them still names. Destroying a Force is a hard delete, so they have to go first.
    /// </para>
    /// <para>
    /// <b>An order issued to this Force is deleted whole</b> - its movement steps, its participants and the order itself - because the Force it was issued to no longer
    /// exists and nothing is left to carry it out. An order issued to <i>another</i> Force that merely listed this one as a participant keeps going, minus that participant:
    /// cancelling somebody else's order because one of its members settled would be a surprise the player never asked for.
    /// </para>
    /// <para>
    /// Staged on the caller's context rather than executed, so it lands inside the founding's transaction with everything else.
    /// </para>
    /// </remarks>
    private static async Task ClearStandingOrdersAsync(AscendantEmpireContext context, int unitInstanceId, CancellationToken cancelToken)
    {
        int[] ownOrderIds = await context.ForceOrders
            .Where(order => order.UnitInstanceId == unitInstanceId)
            .Select(order => order.Id)
            .ToArrayAsync(cancelToken);

        if (ownOrderIds.Length > 0)
        {
            ForceOrderMovementStep[] steps = await context.ForceOrderMovementSteps
                .Where(step => ownOrderIds.Contains(step.ForceOrderId))
                .ToArrayAsync(cancelToken);

            context.ForceOrderMovementSteps.RemoveRange(steps);
        }

        // Covers both cases in one query: every participant row of an order being deleted, and this Force's participation in anybody else's.
        ForceOrderParticipant[] participants = await context.ForceOrderParticipants
            .Where(participant => participant.UnitInstanceId == unitInstanceId || ownOrderIds.Contains(participant.ForceOrderId))
            .ToArrayAsync(cancelToken);

        context.ForceOrderParticipants.RemoveRange(participants);

        if (ownOrderIds.Length > 0)
        {
            ForceOrder[] orders = await context.ForceOrders
                .Where(order => ownOrderIds.Contains(order.Id))
                .ToArrayAsync(cancelToken);

            context.ForceOrders.RemoveRange(orders);
        }
    }

    /// <summary>
    /// Refuses the order unless it is this Player's Turn, and hands back the Turn and Round to stamp the founding with.
    /// </summary>
    /// <remarks>
    /// The same gate <c>StepForceHandler</c> applies, and for the same reason: the toolbar does not check whose Turn it is, so the write path has to. See open question 1 in
    /// <c>docs/design/units/force-context-action-toolbar.md</c>.
    /// </remarks>
    private async Task<(int PlayerTurnId, int RoundNumber, ServiceResult<SettleForceResult>? Refusal)> ValidateItIsThisPlayersTurnAsync(
        SettleForceRequest request,
        CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            const string error = "No turn is in progress in this game, so nothing can be founded.";
            logger.LogWarning("No current campaign turn for Game Id {GameId}, so the settle order by Player Id {PlayerId} was refused. Errors: {Errors}",
                request.GameId, request.PlayerId, string.Join("; ", turnResult.Errors));

            return (0, 0, ServiceResult<SettleForceResult>.Fail(error));
        }

        CurrentCampaignTurn currentTurn = turnResult.SuccessValue;

        if (currentTurn.PlayerId != request.PlayerId)
        {
            string error = $"It is {currentTurn.FactionName}'s turn, so you cannot found a settlement yet.";
            logger.LogInformation("Player Id {PlayerId} tried to settle during Player Id {CurrentPlayerId}'s turn in Game Id {GameId}.",
                request.PlayerId, currentTurn.PlayerId, request.GameId);

            return (0, 0, ServiceResult<SettleForceResult>.Forbidden(error));
        }

        if (currentTurn.PlayerTurnId is not int playerTurnId)
        {
            const string error = "Your turn has not been opened properly, so the founding cannot be recorded.";
            logger.LogError("Game Id {GameId} has a current campaign turn for Player Id {PlayerId} but no open Player Turn to record a founding against.",
                request.GameId, request.PlayerId);

            return (0, 0, ServiceResult<SettleForceResult>.Fail(error));
        }

        return (playerTurnId, currentTurn.CurrentRoundNumber, null);
    }
}
