using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <summary>
/// Owns the write side of the Campaign Turn Queue: opening a Round, handing a Turn to a Player, and starting a Game.
/// </summary>
/// <remarks>
/// <para>
/// This sits alongside <see cref="ICampaignTurnService"/>, which only ever reads. Keeping the writes here is what lets the
/// two callers that need to open a Round - the Game starting for the first time, and the end of every Round after that -
/// share one implementation rather than each stamping the Queue their own way.
/// </para>
/// <para>
/// Every method takes the caller's <see cref="AscendantEmpireContext"/> rather than making its own, so the work joins
/// whatever transaction the caller has open. That matters for <see cref="StartGameAsync"/>, which runs inside the same
/// transaction as the player definition that triggered it.
/// </para>
/// </remarks>
public interface ICampaignRoundService
{
    /// <summary>
    /// Opens a Round on the Turn Queue and starts the Turn of the Player who leads it.
    /// </summary>
    /// <remarks>
    /// This is a complete hand-over: on success exactly one active Participant is
    /// <see cref="TurnQueueParticipantStatus.TakingTurn"/>, every other active Participant is
    /// <see cref="TurnQueueParticipantStatus.Pending"/>, and the Queue's Turn clock is running.
    /// Callers do not need to follow it with anything.
    /// </remarks>
    /// <param name="context">The database context the changes are written through.</param>
    /// <param name="turnQueueId">The Turn Queue whose Round is being opened.</param>
    /// <param name="roundNumber">The number of the Round being opened.</param>
    /// <param name="gameId">The Game the Turn Queue belongs to, for the start-of-round pipeline context.</param>
    Task<ServiceResultBase> StartRoundAsync(AscendantEmpireContext context, int turnQueueId, int roundNumber, int gameId);

    /// <summary>
    /// Hands the Turn to a Player: runs the start-of-turn pipeline, marks them as taking their Turn, and starts the Turn clock.
    /// </summary>
    /// <param name="context">The database context the changes are written through.</param>
    /// <param name="newTurn">The Turn being started, as resolved by <see cref="ICampaignTurnService"/>.</param>
    Task<ServiceResultBase> StartNextTurnAsync(AscendantEmpireContext context, CurrentCampaignTurn newTurn);

    /// <summary>
    /// Starts a Game every one of whose Players has been claimed and defined: moves it to
    /// <see cref="GamePlayStatus.InProgress"/> and opens its first Round.
    /// </summary>
    /// <remarks>
    /// Idempotent. A Game that is already past <see cref="GamePlayStatus.AwaitingPlayers"/> is left
    /// exactly as it is and the call still succeeds, so two callers racing on the last definition cannot start the same
    /// Game twice - and a Player re-saving their definition once play has begun cannot restart it.
    /// </remarks>
    /// <param name="context">The database context the changes are written through, including any open transaction.</param>
    /// <param name="gameId">The Game to start.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    Task<ServiceResultBase> StartGameAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);
}
