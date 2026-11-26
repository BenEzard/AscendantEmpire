using AscendantEmpire.Application.Data;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Works out what one Turn's movement and construction earned, from the history those systems already wrote.
/// </summary>
/// <remarks>
/// Deriving rather than accruing as things happen is what keeps movement and settling free of any knowledge of the Technology feature, and it is what makes
/// "once per tile per Turn" free: <c>MovementHistory</c> already holds every step, so one <c>Distinct</c> says what the Player actually learned.
/// </remarks>
internal interface IExperienceAccrualReader
{
    /// <summary>
    /// The experience the Player earned during one Turn by crossing ground and by finishing Settlements, as a batch for <c>IExperienceAllocator</c>.
    /// </summary>
    /// <returns>Points keyed by <c>ExperienceType.Code</c>. Empty where the Turn earned nothing.</returns>
    Task<IReadOnlyDictionary<string, int>> ReadForTurnAsync(
        AscendantEmpireContext context,
        int playerId,
        int playerTurnId,
        CancellationToken cancelToken);
}
