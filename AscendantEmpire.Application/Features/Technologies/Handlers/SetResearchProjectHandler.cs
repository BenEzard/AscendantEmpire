using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record SetResearchProjectRequest(int GameId, int PlayerId, int? TechnologyId) : IRequest<ServiceResultBase>;

/// <summary>
/// Selects a Player's directed research at a technology, paying its acquisition cost on the way if it is free.
/// </summary>
/// <remarks>
/// <para>
/// Directed research requires <see cref="TechnologyStatus.Unlocked"/>, which means prerequisites discovered <i>and</i> the acquisition cost settled - so
/// choosing an <see cref="TechnologyStatus.Available"/> technology has to settle that cost before it can become the project. The design doc has a free
/// technology moving straight from available to unlocked, and the catalogue authors no acquisition costs at all, so in practice every choice takes that
/// path. A technology that does carry cost rows is refused rather than unlocked for nothing: there is no Asset stockpile to spend from yet, and silently
/// waiving a cost would be a rule invented here.
/// </para>
/// <para>
/// Read-modify-write on the one row, not <c>ExecuteUpdateAsync</c>. The bulk form is what <see cref="PlayerTechnology.DirectedPoints"/> calls for when
/// points accrue, where a read-modify-write would lose a concurrent increment; stamping a status on a single row the calling Player owns has no such race.
/// </para>
/// </remarks>
internal class SetResearchProjectHandler(
    ILogger<SetResearchProjectHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<SetResearchProjectRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(
        SetResearchProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            PlayerResearchState? researchState = await context.PlayerResearchStates
                .FirstOrDefaultAsync(state => state.PlayerId == request.PlayerId, cancellationToken);

            if (researchState is null)
            {
                return ServiceResultBase.NotFound(
                    $"Player {request.PlayerId} has no research state. One is written for every Player at game setup, so this Player has not been set up.");
            }

            // Clearing is a real choice, not a no-op: a Player may legitimately go back to researching nothing, and the end-turn check reports that state.
            if (request.TechnologyId is null)
            {
                researchState.CurrentTechnologyId = null;

                await context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Cleared the research project for player {PlayerId}.", request.PlayerId);

                return ServiceResultBase.Succeed();
            }

            int technologyId = request.TechnologyId.Value;

            PlayerTechnology? playerTechnology = await context.PlayerTechnologies
                .Include(row => row.Technology)
                .FirstOrDefaultAsync(
                    row => row.PlayerId == request.PlayerId && row.TechnologyId == technologyId,
                    cancellationToken);

            if (playerTechnology is null)
            {
                // A row exists for every Player and every technology from game setup onwards, so a missing one means the technology was seeded after this
                // game was created and nothing has reconciled since. The availability reconcile backfills it; this read-side handler deliberately does not.
                return ServiceResultBase.NotFound(
                    $"Player {request.PlayerId} has no row for technology {technologyId}. It was most likely seeded after this game was set up, and the "
                    + "technology availability reconcile has not run since.");
            }

            string technologyName = playerTechnology.Technology.DisplayName;

            switch (playerTechnology.Status)
            {
                case TechnologyStatus.Locked:
                    return ServiceResultBase.Fail($"{technologyName} cannot be researched: its prerequisites have not been discovered.");

                case TechnologyStatus.Discovered:
                    return ServiceResultBase.Fail($"{technologyName} has already been discovered, so there is nothing left to research.");

                case TechnologyStatus.Available:
                    ServiceResultBase unlockResult = await UnlockIfFreeAsync(context, playerTechnology, technologyName, request.GameId, cancellationToken);

                    if (!unlockResult.HasSucceeded)
                    {
                        return unlockResult;
                    }

                    break;

                case TechnologyStatus.Unlocked:
                    // Already paid for in an earlier Round. Re-stamping the Round would rewrite when the cost was settled, which is not what choosing it
                    // again means.
                    break;
            }

            researchState.CurrentTechnologyId = technologyId;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Set the research project for player {PlayerId} to technology {TechnologyId} ({TechnologyName}).",
                request.PlayerId,
                technologyId,
                technologyName);

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while setting the research project.";
            logger.LogError(ex, error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// Settles a free technology's acquisition cost and moves it to <see cref="TechnologyStatus.Unlocked"/>.
    /// </summary>
    /// <remarks>
    /// Stamping the Round is the unlock event - there is no separate UnlockedOnRound column - and the database insists on it:
    /// <c>CK_PlayerTechnology_StatusStamps</c> requires an Unlocked row to carry an <c>AcquisitionPaidOnRound</c> and no <c>DiscoveredOnRound</c>. A
    /// technology with no cost rows still stamps the Round it unlocked, because the cost was zero rather than unpaid.
    /// </remarks>
    private static async Task<ServiceResultBase> UnlockIfFreeAsync(
        AscendantEmpireContext context,
        PlayerTechnology playerTechnology,
        string technologyName,
        int gameId,
        CancellationToken cancellationToken)
    {
        string[] costs = await context.TechnologyAcquisitionCosts
            .AsNoTracking()
            .Where(cost => cost.TechnologyId == playerTechnology.TechnologyId)
            .OrderBy(cost => cost.Asset.DisplayName)
            .Select(cost => $"{cost.Quantity} x {cost.Asset.DisplayName}")
            .ToArrayAsync(cancellationToken);

        if (costs.Length > 0)
        {
            return ServiceResultBase.Fail(
                $"{technologyName} costs {string.Join(", ", costs)} to unlock, and acquisition costs cannot be paid yet. "
                + "Nothing spends from a Player's Asset stockpile, so the cost would have to be waived rather than settled.");
        }

        int? currentRound = await context.TurnQueues
            .AsNoTracking()
            .Where(queue => queue.GameId == gameId && queue.QueueType == TurnQueueType.Campaign)
            .Select(queue => (int?)queue.CurrentRoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentRound is null)
        {
            return ServiceResultBase.NotFound($"Game {gameId} has no campaign turn queue, so its current Round is unknown.");
        }

        playerTechnology.AcquisitionPaidOnRound = currentRound.Value;
        playerTechnology.Status = TechnologyStatus.Unlocked;

        return ServiceResultBase.Succeed();
    }
}
