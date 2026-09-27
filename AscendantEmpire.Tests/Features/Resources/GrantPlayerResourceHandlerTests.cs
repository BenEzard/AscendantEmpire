using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.Domain.Entities.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Resources;

/// <summary>
/// Tests the development-only handler that grants a Player resource unlocks, standing in for the Technology system.
/// </summary>
/// <remarks>See <see cref="PlayerResourceUnlockTestBase"/> for the tier and the seeded fixture.</remarks>
[TestFixture]
internal class GrantPlayerResourceHandlerTests : PlayerResourceUnlockTestBase
{
    private GrantPlayerResourceHandler _handler = null!;

    [SetUp]
    public void CreateHandler()
    {
        _handler = new GrantPlayerResourceHandler(NullLogger<GrantPlayerResourceHandler>.Instance, DbFactory);
    }

    [Test, Description("A granted resource and meta-category are stamped with the game's current round, not zero, so a mid-game unlock records when it happened.")]
    public async Task ShouldGrantAtTheCurrentRound()
    {
        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        PlayerUnlockedResource resource = await verify.PlayerUnlockedResources.SingleAsync();
        PlayerUnlockedResourceMetaCategory metaCategory = await verify.PlayerUnlockedResourceMetaCategories.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resource.UnlockedOnRound, Is.EqualTo(_CURRENT_ROUND));
            Assert.That(resource.ObsoleteOnRound, Is.Null);
            Assert.That(metaCategory.UnlockedOnRound, Is.EqualTo(_CURRENT_ROUND));
        });
    }

    [Test, Description("Granting twice updates the existing rows rather than inserting duplicates, which the composite key would refuse.")]
    public async Task ShouldNotDuplicateWhenGrantedTwice()
    {
        await SendAsync();
        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await verify.PlayerUnlockedResources.CountAsync(), Is.EqualTo(1));
            Assert.That(await verify.PlayerUnlockedResourceMetaCategories.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test, Description("ObsoleteInRounds is resolved against the current round, so the row stores the future round availability ends rather than the offset itself.")]
    public async Task ShouldScheduleObsolescenceRelativeToTheCurrentRound()
    {
        await SendAsync(obsoleteInRounds: 10);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.That((await verify.PlayerUnlockedResources.SingleAsync()).ObsoleteOnRound, Is.EqualTo(_CURRENT_ROUND + 10));
    }

    [Test, Description("Re-granting without an obsoleting countdown clears one set earlier, so a scheduled retirement can be called off.")]
    public async Task ShouldClearObsolescenceWhenRegrantedWithout()
    {
        await SendAsync(obsoleteInRounds: 10);
        await SendAsync();

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.That((await verify.PlayerUnlockedResources.SingleAsync()).ObsoleteOnRound, Is.Null);
    }

    [Test, Description("An unknown code is reported rather than silently ignored, since a typo would otherwise look identical to a call that changed nothing.")]
    public async Task ShouldFailOnAnUnknownCode()
    {
        ServiceResultBase result = await _handler.Handle(
            new GrantPlayerResourceRequest(_GAME_ID, _playerId, ["NotAResource"], [], ObsoleteInRounds: null),
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
            new GrantPlayerResourceRequest(_GAME_ID, _playerId, [], [], ObsoleteInRounds: null),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("A game with no campaign turn queue fails rather than guessing a round to stamp the unlock with.")]
    public async Task ShouldFailWhenTheGameHasNoCampaignQueue()
    {
        ServiceResultBase result = await _handler.Handle(
            new GrantPlayerResourceRequest(GameId: _GAME_ID + 99, _playerId, [_RESOURCE_CODE], [], ObsoleteInRounds: null),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("campaign turn queue"));
        });
    }

    private async Task SendAsync(int? obsoleteInRounds = null)
    {
        ServiceResultBase result = await _handler.Handle(
            new GrantPlayerResourceRequest(_GAME_ID, _playerId, [_RESOURCE_CODE], [_META_CATEGORY_CODE], obsoleteInRounds),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }
}
