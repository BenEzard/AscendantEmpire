using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for the hand-overs <see cref="AssetBankConquestService"/> refuses, and for what a refusal leaves behind.
/// </summary>
/// <remarks>
/// <para>
/// In-memory, per <c>testing.md</c>: every refusal here is settled before the service saves anything, so none of them reaches a query the in-memory provider
/// cannot answer.
/// </para>
/// <para>
/// <b>The hand-over itself is not here.</b> It opens a transaction, which the in-memory provider cannot give it, so what a successful conquest does to the
/// bank and to both players' networks is pinned in <see cref="AssetBankConquestServicePersistenceTests"/> instead - the same split as
/// <c>StructureLifecycleWriterTests</c> and its persistence sibling.
/// </para>
/// </remarks>
[TestFixture]
internal class AssetBankConquestServiceTests : AssetBankTestBase
{
    private const int _SETTLEMENT_ID = 51;
    private const int _SETTLEMENT_WITH_NO_BANK_ID = 52;

    private IAssetNetworkConnectivityService _connectivityService = null!;
    private AssetBankConquestService _service = null!;

    [SetUp]
    public void BuildService()
    {
        _connectivityService = Substitute.For<IAssetNetworkConnectivityService>();

        _connectivityService
            .RecomputeForPlayerAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Succeed());

        _service = new AssetBankConquestService(NullLogger<AssetBankConquestService>.Instance, _connectivityService);
    }

    [Test, Description("A capture naming a Settlement that has no bank is refused as NotFound, because every Settlement is born with one and a missing bank means the wrong Settlement.")]
    public async Task ShouldRefuseACaptureOfASettlementWithNoBankAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.TransferOnConquestAsync(context, _SETTLEMENT_WITH_NO_BANK_ID, _otherPlayerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }

        await AssertConnectivityWasNotTouchedAsync();
    }

    [Test, Description("A hand-over to the Player who already owns the bank is refused, so a conquest is never recorded where nothing changed hands.")]
    public async Task ShouldRefuseAHandOverToTheCurrentOwnerAsync()
    {
        Guid originalKey = Guid.NewGuid();
        int assetBankId = await CreateBankAsync(_playerId, _SETTLEMENT_ID, new Dictionary<string, long> { [_OAK_LOG_CODE] = 4 }, originalKey);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.TransferOnConquestAsync(context, _SETTLEMENT_ID, _playerId);

        AssetBank saved = await ReloadBankAsync(assetBankId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(saved.PlayerId, Is.EqualTo(_playerId));
            Assert.That(saved.NetworkKey, Is.EqualTo(originalKey), "A refused hand-over writes nothing, the key included.");
        }

        await AssertConnectivityWasNotTouchedAsync();
    }

    [Test, Description("The bank is found by its Settlement rather than by its owner, so a capture refuses on the Settlement's real owner and not on the Player who was asked about.")]
    public async Task ShouldFindTheBankBySettlementRatherThanByOwnerAsync()
    {
        // The rival's Settlement, with a bank of the rival's own. Asking to hand it to the rival must refuse, even though the caller is not its owner.
        await CreateBankAsync(_otherPlayerId, _SETTLEMENT_ID, new Dictionary<string, long>());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await _service.TransferOnConquestAsync(context, _SETTLEMENT_ID, _otherPlayerId);

        Assert.That(result.HasSucceeded, Is.False);

        await AssertConnectivityWasNotTouchedAsync();
    }

    /// <summary>
    /// Asserts that a refused hand-over never reached the one writer of <c>NetworkKey</c>, in either of the two ways it could have.
    /// </summary>
    private async Task AssertConnectivityWasNotTouchedAsync()
    {
        _connectivityService.DidNotReceive().AssignKeyToCapturedBank(Arg.Any<AssetBank>());

        await _connectivityService.DidNotReceive()
            .RecomputeForPlayerAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private async Task<AssetBank> ReloadBankAsync(int assetBankId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.AssetBanks.AsNoTracking().SingleAsync(bank => bank.Id == assetBankId);
    }
}
