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
/// Reads every asset bank a Player owns, which together are everything they hold anywhere.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, checked so a request cannot be answered out of whatever banks its Player id happens to own.</param>
public record GetPlayerAssetBanksRequest(int GameId, int PlayerId) : IRequest<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>>;

/// <remarks>
/// Registered and run by <c>ValidationBehaviour</c> before the handler is reached, so the handler does not invoke it itself.
/// </remarks>
internal class GetPlayerAssetBanksRequestValidator : Validator<GetPlayerAssetBanksRequest>
{
    public GetPlayerAssetBanksRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

/// <summary>
/// The all-banks read of the two the design doc calls for, which is what a summary ticker adds up.
/// </summary>
/// <remarks>
/// <para>
/// A bank per pool rather than one merged set of totals. Two banks exist precisely because their Settlements cannot reach each other, so a single figure
/// would advertise stock that no one build order could spend - see <see cref="AssetBankHoldings"/> for why the memberships come back with the balances.
/// </para>
/// <para>A Player with no settlement yet owns no banks, and that is an empty list rather than a not-found.</para>
/// </remarks>
internal class GetPlayerAssetBanksHandler(
    ILogger<GetPlayerAssetBanksHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IAssetBankReader bankReader
) : IRequestHandler<GetPlayerAssetBanksRequest, ServiceResult<IReadOnlyCollection<AssetBankHoldings>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<AssetBankHoldings>>> Handle(
        GetPlayerAssetBanksRequest request,
        CancellationToken cancellationToken)
    {
        // The same cheap "is this Player part of this in-progress Game" probe the asset tree read starts with.
        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult =
            await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancellationToken);

        if (gameContextResult.SuccessValue is not { Count: > 0 })
        {
            string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<IReadOnlyCollection<AssetBankHoldings>>.NotFound(error);
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await bankReader.GetForPlayerAsync(context, request.PlayerId, cancellationToken);
    }
}
