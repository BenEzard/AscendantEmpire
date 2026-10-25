using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <summary>
/// The predicate that picks out a Game's live campaign lane, in the one place every reader of it takes it from.
/// </summary>
/// <remarks>
/// <para>
/// Four conditions, and leaving any of them off changes the answer. <c>QueueType</c> keeps a battle lane out, and <c>BattleId</c> keeps one out a second
/// way - redundant today, since nothing writes a Campaign queue against a Battle, and kept because a lane that somehow carried both would otherwise be
/// read as the campaign. <c>IsCompleted</c> is the one that matters most: a finished lane still holds a plausible Round number, so a reader that forgets
/// it goes on reporting the Round a Game ended on.
/// </para>
/// <para>
/// A Game has exactly one live campaign lane, so a caller that cannot sensibly go on without it should take the match with <c>SingleOrDefault</c> and
/// report two as the data error it is, rather than quietly taking whichever came back first.
/// </para>
/// <para>
/// Returned untracked-agnostic: a caller projecting a figure adds <c>AsNoTracking</c>, and one that means to write the lane does not.
/// </para>
/// </remarks>
internal static class CampaignTurnQueueQuery
{
    /// <summary>
    /// The Game's live campaign turn queue, as a query to project or filter further.
    /// </summary>
    public static IQueryable<TurnQueue> Active(AscendantEmpireContext context, int gameId)
    {
        return context.TurnQueues
            .Where(queue => queue.GameId == gameId)
            .Where(queue => queue.QueueType == TurnQueueType.Campaign)
            .Where(queue => queue.BattleId == null)
            .Where(queue => queue.IsCompleted == false);
    }
}
