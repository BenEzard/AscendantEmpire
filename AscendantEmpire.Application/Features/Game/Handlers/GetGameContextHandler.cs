using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Games.GetGameContext;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

/// <summary>
/// Retrieve the map geometry and active Players of in-progress Games, optionally narrowed to one Game and/or one Player.
/// </summary>
/// <remarks>
/// <para>
/// This is an internal lookup, not a listing, and it is deliberately not reachable over HTTP. Handlers use it for map dimensions and wrapping, for a
/// Player's campaign map offset, and as a cheap "is this Player part of this in-progress Game" probe.
/// </para>
/// <para>
/// It is not the Open Game dialog's data source. That is <see cref="GetMyGamesRequest"/>, which is scoped to the caller's own account.
/// </para>
/// </remarks>
public record GetGameContextRequest(int? GameId, int? PlayerId) : IRequest<ServiceResult<IReadOnlyCollection<GameContext>>>;

/// <remarks>
/// The query itself lives on <see cref="IGameContextService"/> rather than here, because <c>EndCampaignTurnHandler</c> needs it from inside a handler
/// that is already mid-transaction and so cannot go back through the mediator.
/// </remarks>
internal sealed class GetGameContextHandler(
    IGameContextService gameContextService
) : IRequestHandler<GetGameContextRequest, ServiceResult<IReadOnlyCollection<GameContext>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<GameContext>>> Handle(GetGameContextRequest request, CancellationToken cancelToken)
    {
        return await gameContextService.GetGameContexts(request.GameId, request.PlayerId, cancelToken);
    }
}
