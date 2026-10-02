using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the development-only handler that takes asset unlocks back off a Player, standing in for the Technology system.
/// </summary>
/// <remarks>See <see cref="PlayerAssetUnlockTestBase"/> for the tier and the seeded fixture.</remarks>
[TestFixture]
internal class WithdrawPlayerAssetHandlerTests : PlayerAssetUnlockTestBase
{
    private WithdrawPlayerAssetHandler _handler = null!;

    [SetUp]
    public void CreateHandler()
    {
        _handler = new WithdrawPlayerAssetHandler(NullLogger<WithdrawPlayerAssetHandler>.Instance, DbFactory);
    }

    [Test, Description("Withdrawing removes the rows outright, leaving the Player as though the asset had never been granted rather than as having retired it.")]
    public async Task ShouldRemoveTheRowsRatherThanObsoletingThem()
    {
        await GrantAsync();

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await verify.PlayerUnlockedAssets.AnyAsync(), Is.False);
            Assert.That(await verify.PlayerUnlockedAssetMetaCategories.AnyAsync(), Is.False);
        });
    }

    [Test, Description("Withdrawing something already scheduled to obsolete still removes it, so a countdown does not have to run out first.")]
    public async Task ShouldRemoveARowThatWasAlreadyObsoleting()
    {
        await GrantAsync(obsoleteInRounds: 10);

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.That(await verify.PlayerUnlockedAssets.AnyAsync(), Is.False);
    }

    [Test, Description("Withdrawing something the Player never had succeeds, because the end state is the one asked for either way.")]
    public async Task ShouldSucceedWhenThePlayerDoesNotHaveIt()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerAssetRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], []),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("Only the named rows go, so withdrawing one asset leaves the Player's others alone.")]
    public async Task ShouldLeaveOtherUnlocksAlone()
    {
        await GrantAsync();

        var grant = new GrantPlayerAssetHandler(NullLogger<GrantPlayerAssetHandler>.Instance, DbFactory);
        await grant.Handle(
            new GrantPlayerAssetRequest(_GAME_ID, _playerId, ["Weapons.MeleeWeapons.StoneAxe.Low"], [], ObsoleteInRounds: null),
            CancellationToken.None);

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int survivingAssetId = (await verify.Assets.SingleAsync(r => r.Code == "Weapons.MeleeWeapons.StoneAxe.Low")).Id;

        Assert.That(
            await verify.PlayerUnlockedAssets.Select(u => u.AssetId).ToListAsync(),
            Is.EqualTo(new[] { survivingAssetId }));
    }

    [Test, Description("An unknown code is reported rather than silently ignored, on the same terms as a grant.")]
    public async Task ShouldFailOnAnUnknownCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerAssetRequest(_GAME_ID, _playerId, ["NotAAsset"], []),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("NotAAsset"));
        });
    }

    [Test, Description("A request naming no codes at all is refused, rather than reporting success for having done nothing.")]
    public async Task ShouldFailWhenNoCodesAreSupplied()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerAssetRequest(_GAME_ID, _playerId, [], []),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    private async Task SendAsync()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerAssetRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], [_META_CATEGORY_CODE]),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }
}
