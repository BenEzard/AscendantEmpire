using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetMyGames;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

/// <summary>
/// Lists the in-progress Games the calling User holds an active Player slot in, one row per Game.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token, not the request body. It does two jobs: it filters the listing down to the caller's
/// own games, and it decides which row's campaign turn is flagged as theirs.
/// </para>
/// <para>
/// This is the Open Game dialog's data source. It is not <see cref="GetGameContextRequest"/>, which is an internal geometry lookup that happens to
/// return the same Games.
/// </para>
/// </remarks>
public record GetMyGamesRequest(int UserId) : IRequest<ServiceResult<IReadOnlyCollection<MyGame>>>;

internal class GetMyGamesRequestValidator : Validator<GetMyGamesRequest>
{
    public GetMyGamesRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");
    }
}

internal sealed class GetMyGamesHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetMyGamesRequest, ServiceResult<IReadOnlyCollection<MyGame>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<MyGame>>> Handle(GetMyGamesRequest request, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Driven from the Player side rather than the Game side: the caller holds at most one slot per Game, so starting there gives one row per
        // Game for free, with no grouping to undo afterwards.
        var rows = await (
            from p in context.Players.AsNoTracking()
            where p.UserId == request.UserId
               && p.Status == PlayerStatus.Active
            join gi in context.GameIndex.AsNoTracking()
                on p.GameId equals gi.Id
            where gi.CreationStatus == GameCreationStatus.SetupComplete
               && gi.PlayStatus == GamePlayStatus.InProgress
            select new
            {
                GameId = gi.Id,
                gi.GameName,
                gi.MapWidth,
                gi.MapHeight,

                YourPlayerId = p.Id,
                p.RulerTitle,
                p.RulerName,
                p.FactionName,
                p.CampaignOffsetId,

                // Whoever the Game's campaign lane is currently waiting on. The same predicate CampaignTurnService.GetCurrentCampaignTurnAsync
                // uses, inlined as a correlated subquery so the whole listing is one round trip rather than one query per Game.
                //
                // It has to behave as a left join. A Game sitting between turns, and one whose lane has not started, both have no participant
                // taking a turn, and dropping those Games from the caller's own list would be worse than showing them with no ruler named.
                CurrentTurn = context.TurnQueueParticipants
                    .Where(tqp => tqp.TurnQueue.GameId == gi.Id)
                    .Where(tqp => tqp.TurnQueue.QueueType == TurnQueueType.Campaign)
                    .Where(tqp => tqp.TurnQueue.BattleId == null)
                    .Where(tqp => tqp.TurnQueue.IsCompleted == false)
                    .Where(tqp => tqp.IsActiveInLane)
                    .Where(tqp => tqp.Status == TurnQueueParticipantStatus.TakingTurn)
                    .Select(tqp => new
                    {
                        tqp.PlayerId,
                        tqp.Player.RulerTitle,
                        tqp.Player.RulerName,
                        tqp.Player.FactionName
                    })
                    .FirstOrDefault()
            }
        ).ToListAsync(cancelToken);

        List<MyGame> myGames = [.. rows
            .Select(r => new MyGame
            {
                GameId = r.GameId,
                GameName = r.GameName,
                MapWidth = r.MapWidth,
                MapHeight = r.MapHeight,

                YourPlayerId = r.YourPlayerId,
                YourRulerLabel = $"{r.RulerTitle} {r.RulerName} ({r.FactionName})",
                YourCampaignOffsetId = r.CampaignOffsetId,

                CurrentTurnPlayerId = r.CurrentTurn?.PlayerId,
                CurrentTurnRulerLabel = r.CurrentTurn == null
                    ? null
                    : $"{r.CurrentTurn.RulerTitle} {r.CurrentTurn.RulerName} ({r.CurrentTurn.FactionName})",
                IsYourTurn = r.CurrentTurn != null && r.CurrentTurn.PlayerId == r.YourPlayerId
            })
            .OrderBy(g => g.GameName)];

        return ServiceResult<IReadOnlyCollection<MyGame>>.Succeed(myGames);
    }
}
