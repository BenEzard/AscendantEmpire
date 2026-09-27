using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Resources.GetPlayerResources;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Resources;

/// <summary>
/// Tests the read that builds a Player's visible resource tree for the Resources toolbar.
/// </summary>
/// <remarks>
/// See <see cref="PlayerResourceUnlockTestBase"/> for the tier and the seeded fixture. The handler's game-context guard goes through the mediator, which
/// is substituted here rather than wired up: what it proves is only that the Player belongs to an in-progress Game, and that has its own coverage.
/// </remarks>
[TestFixture]
internal class GetPlayerResourcesHandlerTests : PlayerResourceUnlockTestBase
{
    // The grouped arm of the catalogue: a meta-category holding grouped categories, one of which holds the Logs group.
    private const string _META_GROUPED = "ConstructionMaterials";
    private const string _CATEGORY_WOOD = "ConstructionMaterials.Wood";
    private const string _GROUP_LOGS = "ConstructionMaterials.Wood.Logs";
    private const string _GROUP_PLANKS = "ConstructionMaterials.Wood.Planks";
    private const string _RESOURCE_OAK_LOG = "ConstructionMaterials.Wood.Logs.OakLog";
    private const string _RESOURCE_CEDAR_LOG = "ConstructionMaterials.Wood.Logs.CedarLog";
    private const string _RESOURCE_OAK_PLANK = "ConstructionMaterials.Wood.Planks.OakPlank";

    // The standalone arm: Weapons holds one standalone category whose Resources hang off it directly. The base's _RESOURCE_CODE is one of them.
    private const string _META_STANDALONE = "Weapons";
    private const string _CATEGORY_MELEE = "Weapons.MeleeWeapons";

    // A third meta-category, used whenever a test needs one with nothing granted beneath it.
    private const string _META_EMPTY = "RawMaterials";

    private GetPlayerResourcesHandler _handler = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public void CreateHandler()
    {
        _mediator = Substitute.For<IMediator>();
        StubGameContext(playerIsInGame: true);

        _handler = new GetPlayerResourcesHandler(NullLogger<GetPlayerResourcesHandler>.Instance, _mediator, DbFactory);
    }

    [Test, Description("A Player who has unlocked nothing gets an empty tree rather than the whole catalogue, since the unlock rows are the only gate on visibility.")]
    public async Task ShouldReturnNothingWhenNoMetaCategoryIsUnlocked()
    {
        IReadOnlyCollection<PlayerResourceMetaCategory> tree = await SendAsync();

        Assert.That(tree, Is.Empty);
    }

    [Test, Description("An unlocked meta-category with nothing available beneath it still appears, with no categories: hiding something the Player has been granted would be stranger than showing it empty.")]
    public async Task ShouldReturnTheMetaCategoryWhenItIsUnlockedButHoldsNothingVisible()
    {
        await GrantAsync([], [_META_EMPTY]);

        PlayerResourceMetaCategory metaCategory = Single(await SendAsync());

        Assert.Multiple(() =>
        {
            Assert.That(metaCategory.Code, Is.EqualTo(_META_EMPTY));
            Assert.That(metaCategory.Categories, Is.Empty);
            Assert.That(metaCategory.IconPath, Is.Not.Empty, "the toolbar icon is the whole point of returning an empty meta-category");
        });
    }

    [Test, Description("A Resource on the grouped arm is nested meta-category to category to group to resource, placed by its ResourceGroupId rather than the denormalised meta-category column.")]
    public async Task ShouldNestAGroupedResourceUnderItsGroupAndCategory()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        PlayerResourceCategory category = Single(Single(await SendAsync()).Categories);
        PlayerResourceGroup group = Single(category.Groups);
        PlayerResource resource = Single(group.Resources);

        Assert.Multiple(() =>
        {
            Assert.That(category.Code, Is.EqualTo(_CATEGORY_WOOD));
            Assert.That(category.CategoryType, Is.EqualTo(ResourceCategoryType.GroupedResources));
            Assert.That(category.Resources, Is.Empty, "a grouped category carries its Resources through its groups, never directly");
            Assert.That(group.Code, Is.EqualTo(_GROUP_LOGS));
            Assert.That(resource.Code, Is.EqualTo(_RESOURCE_OAK_LOG));
        });
    }

    [Test, Description("A Resource on the standalone arm hangs off its category with no group between, and the category reports StandaloneResources so the client picks the flat layout.")]
    public async Task ShouldPlaceAStandaloneResourceDirectlyOnItsCategory()
    {
        await GrantAsync([_RESOURCE_CODE], [_META_STANDALONE]);

        PlayerResourceCategory category = Single(Single(await SendAsync()).Categories);

        Assert.Multiple(() =>
        {
            Assert.That(category.Code, Is.EqualTo(_CATEGORY_MELEE));
            Assert.That(category.CategoryType, Is.EqualTo(ResourceCategoryType.StandaloneResources));
            Assert.That(category.Groups, Is.Empty);
            Assert.That(Single(category.Resources).Code, Is.EqualTo(_RESOURCE_CODE));
        });
    }

    [Test, Description("ObsoleteInRounds counts forward from the current Round, because ObsoleteOnRound is the Round availability ends rather than a marker that it already has.")]
    public async Task ShouldReportTheRoundsRemainingWhenAResourceIsObsoleting()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED], obsoleteInRounds: 4);

        Assert.That(FirstResource(await SendAsync()).ObsoleteInRounds, Is.EqualTo(4));
    }

    [Test, Description("A Resource with no scheduled retirement reports a null countdown rather than zero, which a client would read as obsoleting this Round.")]
    public async Task ShouldReportNoCountdownWhenAResourceIsNotObsoleting()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        Assert.That(FirstResource(await SendAsync()).ObsoleteInRounds, Is.Null);
    }

    [Test, Description("A Resource whose obsoleting Round has passed drops out entirely, so a stale unlock row cannot keep a retired Resource on the panel.")]
    public async Task ShouldHideTheResourceWhenItsObsoletingRoundHasPassed()
    {
        await GrantAsync([], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("The Round availability ends is exclusive, so a Resource is gone on the Round named rather than surviving it.")]
    public async Task ShouldHideTheResourceWhenItsObsoletingRoundIsTheCurrentRound()
    {
        await GrantAsync([], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG], [], obsoleteInRounds: 0);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("The same availability rule governs the meta-category unlock row, so an obsoleted meta-category takes its whole subtree off the panel.")]
    public async Task ShouldHideTheMetaCategoryWhenItsObsoletingRoundHasPassed()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], []);
        await GrantAsync([], [_META_GROUPED], obsoleteInRounds: -1);

        Assert.That(await SendAsync(), Is.Empty);
    }

    [Test, Description("A Resource unlocked beneath a meta-category the Player has not unlocked stays hidden: the meta-category is the outer gate, not the Resource.")]
    public async Task ShouldHideResourcesBeneathAMetaCategoryThatIsNotUnlocked()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_STANDALONE]);

        Assert.That(Single(await SendAsync()).Code, Is.EqualTo(_META_STANDALONE), "only the unlocked meta-category is returned");
    }

    [Test, Description("A group has no unlock row of its own, so it goes away with the last available Resource in it rather than showing an empty row.")]
    public async Task ShouldDropTheGroupWhenObsolescenceEmptiesIt()
    {
        await GrantAsync([_RESOURCE_OAK_PLANK], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG], [], obsoleteInRounds: -1);

        PlayerResourceCategory category = Single(Single(await SendAsync()).Categories);

        Assert.That(Single(category.Groups).Code, Is.EqualTo(_GROUP_PLANKS), "the Logs group held only the obsoleted Resource");
    }

    [Test, Description("A grouped category is visible only while it holds a visible group, so emptying its last group removes the category too.")]
    public async Task ShouldDropTheCategoryWhenObsolescenceEmptiesItsOnlyGroup()
    {
        await GrantAsync([], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("A standalone category is visible only while it holds a visible Resource, which is the same rule reached by the other arm of the catalogue's exclusive-or.")]
    public async Task ShouldDropTheStandaloneCategoryWhenObsolescenceEmptiesIt()
    {
        await GrantAsync([], [_META_STANDALONE]);
        await GrantAsync([_RESOURCE_CODE], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("Every level comes back in SortOrder, since the client renders the collections in the order given and has no ordering of its own.")]
    public async Task ShouldOrderMetaCategoriesCategoriesAndResourcesBySortOrder()
    {
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG, _RESOURCE_CODE], [_META_GROUPED, _META_STANDALONE]);

        IReadOnlyCollection<PlayerResourceMetaCategory> tree = await SendAsync();
        PlayerResource[] logs = [.. Single(Single(tree.First().Categories).Groups).Resources];

        Assert.Multiple(() =>
        {
            Assert.That(tree.Select(m => m.SortOrder), Is.Ordered);
            Assert.That(tree.SelectMany(m => m.Categories).Select(c => c.SortOrder), Is.Ordered);
            Assert.That(logs.Select(r => r.SortOrder), Is.Ordered);
        });
    }

    [Test, Description("ResourceGroup.SortOrder is nullable, and an unnumbered group sorts last by name - a plain ascending sort would put it first, the opposite of the documented intent.")]
    public async Task ShouldSortAnUnnumberedGroupAfterTheNumberedOnes()
    {
        // The seed always numbers a group, so the null has to be written in by hand.
        await using (AscendantEmpireContext arrange = await DbFactory.CreateDbContextAsync())
        {
            ResourceGroup logs = await arrange.ResourceGroups.SingleAsync(g => g.Code == _GROUP_LOGS);
            logs.SortOrder = null;
            await arrange.SaveChangesAsync();
        }

        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_OAK_PLANK], [_META_GROUPED]);

        PlayerResourceCategory category = Single(Single(await SendAsync()).Categories);

        Assert.That(category.Groups.Select(g => g.Code), Is.EqualTo(new[] { _GROUP_PLANKS, _GROUP_LOGS }));
    }

    [Test, Description("The seven unsourced fields come back as zeros and falses, so a client wiring against the real shape cannot mistake a placeholder for measured data.")]
    public async Task ShouldReturnEveryPlaceholderFieldAtItsUnsourcedDefault()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        PlayerResource resource = FirstResource(await SendAsync());

        Assert.Multiple(() =>
        {
            Assert.That(resource.Amount, Is.Zero);
            Assert.That(resource.AmountAddedPerTurn, Is.Zero);
            Assert.That(resource.HasStrategicStockpile, Is.False);
            Assert.That(resource.IsImported, Is.False);
            Assert.That(resource.IsExported, Is.False);
            Assert.That(resource.DemandNotMet, Is.False);
            Assert.That(resource.IsLuxury, Is.False);
        });
    }

    [Test, Description("A request naming a Player who is not in an in-progress Game is refused rather than answered from whatever unlock rows that Player id happens to have.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGame()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);
        StubGameContext(playerIsInGame: false);

        ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>> result = await HandleAsync();

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Without a current Round the countdowns cannot be computed, so the request fails rather than defaulting to a Round that would silently hide every obsoleting Resource.")]
    public async Task ShouldReturnNotFoundWhenTheGameHasNoCampaignQueue()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        await using (AscendantEmpireContext arrange = await DbFactory.CreateDbContextAsync())
        {
            arrange.TurnQueues.RemoveRange(arrange.TurnQueues);
            await arrange.SaveChangesAsync();
        }

        ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>> result = await HandleAsync();

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    /// <summary>
    /// Runs the handler and asserts it succeeded, returning the tree.
    /// </summary>
    private async Task<IReadOnlyCollection<PlayerResourceMetaCategory>> SendAsync()
    {
        ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>> result = await HandleAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private Task<ServiceResult<IReadOnlyCollection<PlayerResourceMetaCategory>>> HandleAsync()
    {
        return _handler.Handle(new GetPlayerResourcesRequest(_GAME_ID, _playerId), CancellationToken.None);
    }

    /// <summary>
    /// Points the substituted mediator's game-context probe at an in-progress Game holding the fixture's Player, or at nothing.
    /// </summary>
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

    /// <summary>
    /// The one element of a collection a test expects to hold exactly one, reported as an assertion failure rather than an exception when it does not.
    /// </summary>
    private static T Single<T>(IReadOnlyCollection<T> items)
    {
        Assert.That(items, Has.Count.EqualTo(1));

        return items.First();
    }

    /// <summary>
    /// The first Resource in the tree, whichever arm of the catalogue it arrived by.
    /// </summary>
    private static PlayerResource FirstResource(IReadOnlyCollection<PlayerResourceMetaCategory> tree)
    {
        PlayerResource[] resources =
        [
            .. tree
                .SelectMany(metaCategory => metaCategory.Categories)
                .SelectMany(category => category.Resources.Concat(category.Groups.SelectMany(group => group.Resources)))
        ];

        Assert.That(resources, Is.Not.Empty);

        return resources.First();
    }
}
