using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.ForceOrders.SplitForce;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request to divide one unled Force into several smaller ones where it stands.
/// </summary>
/// <remarks>
/// <para>
/// The game and player come from the caller's game token, so a split order cannot be aimed at another player's Forces. The tile is never sent: the groups
/// stand where the Force already stood.
/// </para>
/// <para>
/// <c>GroupSizes</c> is the whole parameterisation of the split, in the order the player composed it. The <b>first</b> group is the one the original Force
/// keeps. Casualty counts are deliberately absent — see <see cref="UnledSplitRules.ApportionInjured"/>.
/// </para>
/// </remarks>
public record SplitForceRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId,
    IReadOnlyCollection<int> GroupSizes
) : IRequest<ServiceResult<SplitForceResult>>;

internal class SplitForceRequestValidator : Validator<SplitForceRequest>
{
    public SplitForceRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");

        // Only the shape of the list is judged here. Whether these sizes are a legal division of *this* Force needs the Force's strength and its tag's
        // quantum, which the handler loads — see UnledSplitRules.
        RuleFor(x => x.GroupSizes)
            .NotNull().WithMessage("The group sizes to split into are required.")
            .Must(groupSizes => groupSizes != null && groupSizes.Count > 0).WithMessage("The group sizes to split into are required.");

        RuleForEach(x => x.GroupSizes)
            .GreaterThan(0).WithMessage("Every group size must be greater than zero.");
    }
}

/// <summary>
/// Divides an unled Force into the groups the player composed, immediately and at the cost of its whole Turn.
/// </summary>
/// <remarks>
/// <para>
/// The original Force <b>survives</b>, holding the first group, rather than being destroyed and recreated. That keeps its identity, its history and its
/// standing orders intact — and the completed order row written below needs a surviving Force to hang off, since <c>ForceOrder.UnitInstanceId</c> is a real
/// foreign key.
/// </para>
/// <para>
/// A <c>ForceOrder</c> is written as a completed audit record, with the created Forces as <c>Result</c> participants, because that row is what the event log
/// reads — see <c>docs/plans/force-instances-roadmap.md</c>, Phase 3.
/// </para>
/// </remarks>
internal class SplitForceHandler(
    ILogger<SplitForceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTurnService campaignTurnService,
    IForceContextActionService contextActionService,
    IForceLifecycleWriter forceLifecycleWriter
) : IRequestHandler<SplitForceRequest, ServiceResult<SplitForceResult>>
{
    public async Task<ServiceResult<SplitForceResult>> Handle(SplitForceRequest request, CancellationToken cancelToken)
    {
        var validator = new SplitForceRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(SplitForceRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<SplitForceResult>.Fail(errors.Prepend(error));
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Tracked: the original's counts are reduced through the change tracker, and the new Forces are added to this same context so one transaction covers
        // both. Its tile comes along for the coordinates on the response, and the compiled snapshot's organisation tag for the authored quantum the
        // requested sizes are judged against.
        UnitInstance? force = await context.UnitInstances
            .Include(f => f.CampaignMap)
            .Include(f => f.UnitTemplate)
                .ThenInclude(t => t.Compiled!)
                .ThenInclude(c => c.OrganisationTag)
            .FirstOrDefaultAsync(f => f.Id == request.UnitInstanceId && f.GameId == request.GameId, cancelToken);

        if (force is null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} is not in this game.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found in Game Id {GameId}.", request.UnitInstanceId, request.GameId);

            return ServiceResult<SplitForceResult>.NotFound(error);
        }

        if (force.PlayerId != request.PlayerId)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} does not belong to you.";
            logger.LogWarning("Player Id {PlayerId} tried to split Unit Instance Id {UnitInstanceId}, which belongs to Player Id {OwnerPlayerId}.",
                request.PlayerId, request.UnitInstanceId, force.PlayerId);

            return ServiceResult<SplitForceResult>.Forbidden(error);
        }

        (int playerTurnId, int roundNumber, ServiceResult<SplitForceResult>? turnRefusal) = await ValidateItIsThisPlayersTurnAsync(request, cancelToken);

        if (turnRefusal is not null)
        {
            return turnRefusal;
        }

        // The rules themselves, re-run against freshly loaded state rather than trusting the toolbar that rendered the button.
        ServiceResult<ForceActionAvailability> availabilityResult = await contextActionService.EvaluateActionAsync(
            request.GameId,
            request.PlayerId,
            [request.UnitInstanceId],
            ForceActionType.SplitForce,
            cancelToken);

        if (availabilityResult.HasSucceeded == false || availabilityResult.SuccessValue is null)
        {
            const string error = "The split order could not be judged against the force's current situation.";
            logger.LogWarning("Could not evaluate {ActionType} for Unit Instance Id {UnitInstanceId}: {Errors}",
                ForceActionType.SplitForce, request.UnitInstanceId, string.Join("; ", availabilityResult.Errors));

            return ServiceResult<SplitForceResult>.Fail(availabilityResult.Errors.Prepend(error));
        }

        ForceActionAvailability availability = availabilityResult.SuccessValue;

        if (availability.IsEnabled == false)
        {
            // The strategy's own wording, which is already written to be shown to the player as it stands.
            logger.LogInformation("Refused {ActionType} for Unit Instance Id {UnitInstanceId}: {Reasons}",
                ForceActionType.SplitForce, request.UnitInstanceId, string.Join("; ", availability.DisabledReasons));

            return ServiceResult<SplitForceResult>.Fail(availability.DisabledReasons);
        }

        // The strategy says this Force *can* split; this says whether the sizes asked for are a division it is allowed to take.
        int totalStrength = force.HealthyCount + force.InjuredCount;
        List<int> groupSizes = [.. request.GroupSizes];

        List<string> sizeErrors = UnledSplitRules.ValidateGroupSizes(force.UnitTemplate.Compiled?.OrganisationTag, totalStrength, groupSizes);

        if (sizeErrors.Count > 0)
        {
            logger.LogInformation("Refused a split of Unit Instance Id {UnitInstanceId} into [{GroupSizes}]: {Reasons}",
                request.UnitInstanceId, string.Join(", ", groupSizes), string.Join("; ", sizeErrors));

            return ServiceResult<SplitForceResult>.Fail(sizeErrors);
        }

        List<int> injuredCounts = UnledSplitRules.ApportionInjured(groupSizes, force.InjuredCount);

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        // The Force the player was holding keeps the first group. Splitting costs the whole Turn, which is the gate SplitForceStrategy already assumes.
        force.HealthyCount = groupSizes[0] - injuredCounts[0];
        force.InjuredCount = injuredCounts[0];
        // Re-based on what the Force came out of the split with, not what it was raised at: the soldiers in the other groups left with them, so a survivor
        // still claiming the whole 200 would read as a Force that had lost three quarters of its strength to casualties.
        force.InitialStrength = groupSizes[0];
        force.CampaignMovementRemaining = 0;

        List<UnitInstance> newForces = [];

        for (int groupIndex = 1; groupIndex < groupSizes.Count; groupIndex++)
        {
            newForces.Add(new UnitInstance
            {
                GameId = force.GameId,
                PlayerId = force.PlayerId,
                CampaignMapTileId = force.CampaignMapTileId,
                UnitTemplateId = force.UnitTemplateId,
                // Its own size, same as the survivor's: every Force a split leaves behind reports the group it holds, so none of them looks depleted.
                InitialStrength = groupSizes[groupIndex],
                HealthyCount = groupSizes[groupIndex] - injuredCounts[groupIndex],
                InjuredCount = injuredCounts[groupIndex],
                CampaignMovementRemaining = 0,
                // Neither the player's name for the Force nor its Command is inherited. A custom name copied onto eight groups names none of them, and a
                // split-off group stands on its own rather than silently joining a Command whose member limit nobody checked.
                Name = null,
                CommandId = null
            });
        }

        // Saves, and joins the transaction opened above, so the reduced original and the new Forces commit together with the rows that record them.
        ServiceResultBase createResult = await forceLifecycleWriter.CreateAsync(context, newForces, playerTurnId, cancelToken);

        if (createResult.HasSucceeded == false)
        {
            await transaction.RollbackAsync(cancelToken);

            const string error = "The split could not be recorded, so the force has been left as it was.";
            logger.LogError("Creating the {NewForceCount} forces for a split of Unit Instance Id {UnitInstanceId} failed: {Errors}",
                newForces.Count, request.UnitInstanceId, string.Join("; ", createResult.Errors));

            return ServiceResult<SplitForceResult>.Fail(createResult.Errors.Prepend(error));
        }

        // Written after the creation, because the Result participants need the new Forces' ids.
        context.ForceOrders.Add(new ForceOrder
        {
            GameId = force.GameId,
            UnitInstanceId = force.Id,
            CommandId = null,
            OrderType = ForceOrderType.SplitForce,
            // A split resolves at once rather than becoming a standing order, so the row is an audit record of something finished.
            OrderStatus = ForceOrderStatus.Completed,
            TargetCampaignMapTileId = force.CampaignMapTileId,
            CreatedTurnNumber = roundNumber,
            CompletedTurnNumber = roundNumber,
            Participants =
            [
                new ForceOrderParticipant { UnitInstanceId = force.Id, Role = ForceOrderParticipantRole.Primary },
                .. newForces.Select(created => new ForceOrderParticipant
                {
                    UnitInstanceId = created.Id,
                    Role = ForceOrderParticipantRole.Result
                })
            ]
        });

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);

        logger.LogInformation("Player Id {PlayerId} split Unit Instance Id {UnitInstanceId} into [{GroupSizes}].",
            request.PlayerId, force.Id, string.Join(", ", groupSizes));

        return ServiceResult<SplitForceResult>.Succeed(new SplitForceResult
        {
            XCoord = force.CampaignMap.XCoord,
            YCoord = force.CampaignMap.YCoord,
            Groups = BuildGroups(force, newForces, groupSizes, injuredCounts)
        });
    }

    /// <summary>
    /// Describes the Forces the split produced, in the order the sizes were asked for, with the original first.
    /// </summary>
    private static List<SplitForceGroup> BuildGroups(
        UnitInstance force,
        IReadOnlyList<UnitInstance> newForces,
        IReadOnlyList<int> groupSizes,
        IReadOnlyList<int> injuredCounts)
    {
        List<SplitForceGroup> groups =
        [
            new SplitForceGroup
            {
                UnitInstanceId = force.Id,
                Size = groupSizes[0],
                HealthyCount = force.HealthyCount,
                InjuredCount = force.InjuredCount,
                IsOriginalForce = true
            }
        ];

        for (int groupIndex = 1; groupIndex < groupSizes.Count; groupIndex++)
        {
            UnitInstance created = newForces[groupIndex - 1];

            groups.Add(new SplitForceGroup
            {
                UnitInstanceId = created.Id,
                Size = groupSizes[groupIndex],
                HealthyCount = created.HealthyCount,
                InjuredCount = injuredCounts[groupIndex],
                IsOriginalForce = false
            });
        }

        return groups;
    }

    /// <summary>
    /// Confirms the caller has an open Turn of their own to split during, and hands back the Turn and round to stamp the records with.
    /// </summary>
    /// <remarks>
    /// The same gate <c>StepForceHandler</c> and <c>SettleForceHandler</c> apply, and for the same reason: the toolbar does not check whose Turn it is, so
    /// the write path has to. <b>This is the third copy of this check</b> — see open question 1 in <c>docs/design/units/force-context-action-toolbar.md</c>.
    /// </remarks>
    private async Task<(int PlayerTurnId, int RoundNumber, ServiceResult<SplitForceResult>? Refusal)> ValidateItIsThisPlayersTurnAsync(
        SplitForceRequest request,
        CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            const string error = "No turn is in progress in this game, so nothing can be split.";
            logger.LogWarning("No current campaign turn for Game Id {GameId}, so the split order by Player Id {PlayerId} was refused. Errors: {Errors}",
                request.GameId, request.PlayerId, string.Join("; ", turnResult.Errors));

            return (0, 0, ServiceResult<SplitForceResult>.Fail(error));
        }

        CurrentCampaignTurn currentTurn = turnResult.SuccessValue;

        if (currentTurn.PlayerId != request.PlayerId)
        {
            string error = $"It is {currentTurn.FactionName}'s turn, so you cannot split a force yet.";
            logger.LogInformation("Player Id {PlayerId} tried to split during Player Id {CurrentPlayerId}'s turn in Game Id {GameId}.",
                request.PlayerId, currentTurn.PlayerId, request.GameId);

            return (0, 0, ServiceResult<SplitForceResult>.Forbidden(error));
        }

        if (currentTurn.PlayerTurnId is not int playerTurnId)
        {
            const string error = "Your turn has not been opened properly, so the split cannot be recorded.";
            logger.LogError("Game Id {GameId} has a current campaign turn for Player Id {PlayerId} but no open Player Turn to record a split against.",
                request.GameId, request.PlayerId);

            return (0, 0, ServiceResult<SplitForceResult>.Fail(error));
        }

        return (playerTurnId, currentTurn.CurrentRoundNumber, null);
    }
}
