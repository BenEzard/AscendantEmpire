using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.BackendContracts.Games.GetGameContext;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Handlers;

/// <summary>
/// Reads every asset bank a Settlement can draw on, which is what that Settlement and everything connected to it can spend.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, checked so a request cannot be answered out of whatever bank its Player id happens to reach.</param>
public record GetSettlementAssetNetworkRequest(int GameId, int PlayerId, int SettlementId) : IRequest<ServiceResult<AssetNetworkHoldings>>;

/// <remarks>
/// Registered and run by <c>ValidationBehaviour</c> before the handler is reached, so the handler does not invoke it itself.
/// </remarks>
internal class GetSettlementAssetNetworkRequestValidator : Validator<GetSettlementAssetNetworkRequest>
{
    public GetSettlementAssetNetworkRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.SettlementId)
            .GreaterThan(0).WithMessage("Settlement Id is required.");
    }
}

/// <summary>
/// The one-network read of the two the design doc calls for: what this Settlement can reach.
/// </summary>
/// <remarks>
/// <para>
/// Keyed on the Settlement rather than on the bank, because that is what a caller has. A Settlement's own bank never changes, but which network it sits in
/// moves as roads are built and pillaged, so the set of banks this returns is not something a client can cache.
/// </para>
/// <para>
/// A Settlement with no bank and a Settlement belonging to another Player are one answer, not-found: a Settlement that is not the Player's is not theirs to
/// ask about, and distinguishing the two would answer a question they did not get to ask.
/// </para>
/// </remarks>
internal class GetSettlementAssetNetworkHandler(
    ILogger<GetSettlementAssetNetworkHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IAssetBankReader bankReader
) : IRequestHandler<GetSettlementAssetNetworkRequest, ServiceResult<AssetNetworkHoldings>>
{
    public async Task<ServiceResult<AssetNetworkHoldings>> Handle(
        GetSettlementAssetNetworkRequest request,
        CancellationToken cancellationToken)
    {
        // Cheap "is this Player part of this in-progress Game" probe
        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult = await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancellationToken);

        if (gameContextResult.SuccessValue is not { Count: > 0 })
        {
            string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<AssetNetworkHoldings>.NotFound(error);
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await bankReader.GetForSettlementAsync(context, request.PlayerId, request.SettlementId, cancellationToken);
    }
}
