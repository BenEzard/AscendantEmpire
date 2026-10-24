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
/// Reads the one asset bank a Settlement draws on, which is what that Settlement and everything connected to it can spend.
/// </summary>
/// <param name="GameId">The Game the Player belongs to, checked so a request cannot be answered out of whatever bank its Player id happens to reach.</param>
public record GetSettlementAssetBankRequest(int GameId, int PlayerId, int SettlementId) : IRequest<ServiceResult<AssetBankHoldings>>;

/// <remarks>
/// Registered and run by <c>ValidationBehaviour</c> before the handler is reached, so the handler does not invoke it itself.
/// </remarks>
internal class GetSettlementAssetBankRequestValidator : Validator<GetSettlementAssetBankRequest>
{
    public GetSettlementAssetBankRequestValidator()
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
/// The one-bank read of the two the design doc calls for: what this Settlement's group holds.
/// </summary>
/// <remarks>
/// <para>
/// Keyed on the Settlement rather than on the bank, because that is what a caller has. Which bank a Settlement draws on moves as roads are built and
/// pillaged, so a bank id held by a client would go stale without anything saying so.
/// </para>
/// <para>
/// A Settlement with no bank and a Settlement belonging to another Player are one answer, not-found: a Settlement that is not the Player's is not theirs to
/// ask about, and distinguishing the two would answer a question they did not get to ask. A camp has no bank of its own either - it draws on the village it
/// is attached to, and the connectivity work is what points its membership there.
/// </para>
/// </remarks>
internal class GetSettlementAssetBankHandler(
    ILogger<GetSettlementAssetBankHandler> logger,
    IMediator mediator,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IAssetBankReader bankReader
) : IRequestHandler<GetSettlementAssetBankRequest, ServiceResult<AssetBankHoldings>>
{
    public async Task<ServiceResult<AssetBankHoldings>> Handle(
        GetSettlementAssetBankRequest request,
        CancellationToken cancellationToken)
    {
        // The same cheap "is this Player part of this in-progress Game" probe the asset tree read starts with.
        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult =
            await mediator.Send(new GetGameContextRequest(request.GameId, request.PlayerId), cancellationToken);

        if (gameContextResult.SuccessValue is not { Count: > 0 })
        {
            string error = $"No in-progress game found for Game Id: {request.GameId} and Player Id: {request.PlayerId}.";
            logger.LogWarning("{Error}", error);

            return ServiceResult<AssetBankHoldings>.NotFound(error);
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await bankReader.GetForSettlementAsync(context, request.PlayerId, request.SettlementId, cancellationToken);
    }
}
