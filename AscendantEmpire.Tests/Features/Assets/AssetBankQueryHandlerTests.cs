using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.BackendContracts.Assets.GetAssetBanks;
using AscendantEmpire.BackendContracts.Games.GetGameContext;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the two reads that put the bank queries on the wire: their game-context guard, and what they hand back.
/// </summary>
/// <remarks>
/// See <see cref="AssetBankTestBase"/> for the tier and the seeded fixture. Both handlers are thin over
/// <see cref="AssetBankReader"/>, which has its own fixture, so the real reader is used here rather than a substitute - what is pinned is the guard and the
/// pass-through. The game-context probe goes through the mediator, which is substituted: what it proves is only that the Player belongs to an in-progress
/// Game, and that has its own coverage.
/// </remarks>
[TestFixture]
internal class AssetBankQueryHandlerTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 41;

    private IMediator _mediator = null!;
    private GetPlayerAssetBanksHandler _playerBanksHandler = null!;
    private GetSettlementAssetBankHandler _settlementBankHandler = null!;

    [SetUp]
    public void CreateHandlers()
    {
        _mediator = Substitute.For<IMediator>();
        StubGameContext(playerIsInGame: true);

        var reader = new AssetBankReader();

        _playerBanksHandler = new GetPlayerAssetBanksHandler(
            NullLogger<GetPlayerAssetBanksHandler>.Instance,
            _mediator,
            DbFactory,
            reader);

        _settlementBankHandler = new GetSettlementAssetBankHandler(
            NullLogger<GetSettlementAssetBankHandler>.Instance,
            _mediator,
            DbFactory,
            reader);
    }

    [Test, Description("The all-banks read hands back every bank the Player owns, so a summary ticker has each pool rather than one merged total.")]
    public async Task ShouldReturnEveryBankThePlayerOwns()
    {
        int firstBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        int secondBankId = await CreateBankAsync(_playerId, [], new Dictionary<string, long> { [_CEDAR_LOG_CODE] = 6 });

        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> result = await _playerBanksHandler.Handle(
            new GetPlayerAssetBanksRequest(_GAME_ID, _playerId),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Select(bank => bank.AssetBankId), Is.EqualTo(new[] { firstBankId, secondBankId }));
        });
    }

    [Test, Description("The one-bank read hands back the bank the named Settlement draws on, with the memberships that say who else can reach it.")]
    public async Task ShouldReturnTheBankTheSettlementDrawsOn()
    {
        int assetBankId = await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });

        ServiceResult<AssetBankHoldings> result = await _settlementBankHandler.Handle(
            new GetSettlementAssetBankRequest(_GAME_ID, _playerId, _SETTLEMENT_ID),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.AssetBankId, Is.EqualTo(assetBankId));
            Assert.That(result.SuccessValue.SettlementIds, Is.EqualTo(new[] { _SETTLEMENT_ID }));
        });
    }

    [Test, Description("The all-banks read is refused when the Player is not in the named in-progress Game, so a stale token cannot be answered out of whatever banks its Player id owns.")]
    public async Task ShouldRefuseTheAllBanksReadWhenThePlayerIsNotInTheGame()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        StubGameContext(playerIsInGame: false);

        ServiceResult<IReadOnlyCollection<AssetBankHoldings>> result = await _playerBanksHandler.Handle(
            new GetPlayerAssetBanksRequest(_GAME_ID, _playerId),
            CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("The one-bank read is refused on the same terms as the all-banks read, so the two cannot disagree about who is allowed to ask.")]
    public async Task ShouldRefuseTheOneBankReadWhenThePlayerIsNotInTheGame()
    {
        await CreateBankAsync(_playerId, [_SETTLEMENT_ID], new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 });
        StubGameContext(playerIsInGame: false);

        ServiceResult<AssetBankHoldings> result = await _settlementBankHandler.Handle(
            new GetSettlementAssetBankRequest(_GAME_ID, _playerId, _SETTLEMENT_ID),
            CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    /// <summary>
    /// Stubs the membership probe both handlers start with.
    /// </summary>
    /// <param name="playerIsInGame">False answers an empty context list, which is how the probe reports a Player who is not in an in-progress Game.</param>
    private void StubGameContext(bool playerIsInGame)
    {
        GameContext[] contexts = playerIsInGame
            ?
            [
                new GameContext
                {
                    GameId = _GAME_ID,
                    GameName = "Test Game",
                    Players =
                    [
                        new GameContextPlayer
                        {
                            PlayerId = _playerId,
                            RulerTitle = "Chief",
                            RulerName = "Tester",
                            FactionName = "Testers",
                            RulerLabel = "Chief Tester (Testers)"
                        }
                    ]
                }
            ]
            : [];

        _mediator
            .Send(Arg.Any<IRequest<ServiceResult<IReadOnlyCollection<GameContext>>>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<GameContext>>.Succeed(contexts));
    }
}
