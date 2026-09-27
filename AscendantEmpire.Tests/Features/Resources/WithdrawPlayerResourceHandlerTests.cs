using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Resources.Handlers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Resources;

/// <summary>
/// Tests the development-only handler that takes resource unlocks back off a Player, standing in for the Technology system.
/// </summary>
/// <remarks>See <see cref="PlayerResourceUnlockTestBase"/> for the tier and the seeded fixture.</remarks>
[TestFixture]
internal class WithdrawPlayerResourceHandlerTests : PlayerResourceUnlockTestBase
{
    private WithdrawPlayerResourceHandler _handler = null!;

    [SetUp]
    public void CreateHandler()
    {
        _handler = new WithdrawPlayerResourceHandler(NullLogger<WithdrawPlayerResourceHandler>.Instance, DbFactory);
    }

    [Test, Description("Withdrawing removes the rows outright, leaving the Player as though the resource had never been granted rather than as having retired it.")]
    public async Task ShouldRemoveTheRowsRatherThanObsoletingThem()
    {
        await GrantAsync();

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await verify.PlayerUnlockedResources.AnyAsync(), Is.False);
            Assert.That(await verify.PlayerUnlockedResourceMetaCategories.AnyAsync(), Is.False);
        });
    }

    [Test, Description("Withdrawing something already scheduled to obsolete still removes it, so a countdown does not have to run out first.")]
    public async Task ShouldRemoveARowThatWasAlreadyObsoleting()
    {
        await GrantAsync(obsoleteInRounds: 10);

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.That(await verify.PlayerUnlockedResources.AnyAsync(), Is.False);
    }

    [Test, Description("Withdrawing something the Player never had succeeds, because the end state is the one asked for either way.")]
    public async Task ShouldSucceedWhenThePlayerDoesNotHaveIt()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerResourceRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], []),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("Only the named rows go, so withdrawing one resource leaves the Player's others alone.")]
    public async Task ShouldLeaveOtherUnlocksAlone()
    {
        await GrantAsync();

        var grant = new GrantPlayerResourceHandler(NullLogger<GrantPlayerResourceHandler>.Instance, DbFactory);
        await grant.Handle(
            new GrantPlayerResourceRequest(_GAME_ID, _playerId, ["Weapons.MeleeWeapons.StoneAxe.Low"], [], ObsoleteInRounds: null),
            CancellationToken.None);

        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int survivingResourceId = (await verify.Resources.SingleAsync(r => r.Code == "Weapons.MeleeWeapons.StoneAxe.Low")).Id;

        Assert.That(
            await verify.PlayerUnlockedResources.Select(u => u.ResourceId).ToListAsync(),
            Is.EqualTo(new[] { survivingResourceId }));
    }

    [Test, Description("An unknown code is reported rather than silently ignored, on the same terms as a grant.")]
    public async Task ShouldFailOnAnUnknownCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerResourceRequest(_GAME_ID, _playerId, ["NotAResource"], []),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("NotAResource"));
        });
    }

    [Test, Description("A request naming no codes at all is refused, rather than reporting success for having done nothing.")]
    public async Task ShouldFailWhenNoCodesAreSupplied()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerResourceRequest(_GAME_ID, _playerId, [], []),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    private async Task SendAsync()
    {
        ServiceResultBase result = await _handler.Handle(
            new WithdrawPlayerResourceRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], [_META_CATEGORY_CODE]),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }
}
