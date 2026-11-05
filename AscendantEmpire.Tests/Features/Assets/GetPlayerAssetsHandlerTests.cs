using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests the read that builds a Player's visible asset tree for the Assets toolbar.
/// </summary>
/// <remarks>
/// See <see cref="PlayerAssetUnlockTestBase"/> for the tier and the seeded fixture. The handler's game-context guard goes through the mediator, which
/// is substituted here rather than wired up: what it proves is only that the Player belongs to an in-progress Game, and that has its own coverage.
/// </remarks>
[TestFixture]
internal class GetPlayerAssetsHandlerTests : PlayerAssetUnlockTestBase
{
    // The grouped arm of the catalogue: a meta-category holding grouped categories, one of which holds the Logs group.
    private const string _META_GROUPED = "ConstructionMaterials";
    private const string _CATEGORY_WOOD = "ConstructionMaterials.Wood";
    private const string _GROUP_LOGS = "ConstructionMaterials.Wood.Logs";
    private const string _GROUP_PLANKS = "ConstructionMaterials.Wood.Planks";
    private const string _RESOURCE_OAK_LOG = "ConstructionMaterials.Wood.Logs.OakLog";
    private const string _RESOURCE_CEDAR_LOG = "ConstructionMaterials.Wood.Logs.CedarLog";
    private const string _RESOURCE_OAK_PLANK = "ConstructionMaterials.Wood.Planks.OakPlank";

    // The standalone arm: Weapons holds one standalone category whose Assets hang off it directly. The base's _RESOURCE_CODE is one of them.
    private const string _META_STANDALONE = "Weapons";
    private const string _CATEGORY_MELEE = "Weapons.MeleeWeapons";

    // A third meta-category, used whenever a test needs one with nothing granted beneath it.
    private const string _META_EMPTY = "RawMaterials";

    // The two scopes the quantity gate treats differently from a banked Asset: a per-Player counter, and a figure derived from a ledger that does not
    // exist yet. Both live under Society.
    private const string _RESOURCE_TECHNOLOGY_EXPERIENCE = "Society.Experience.Technology";
    private const string _RESOURCE_CIVILIANS = "Society.Population.MyPeople.Civilians";

    /// <summary>
    /// What the fixture Player holds of every banked Asset before a test says otherwise. Any positive figure does; one keeps the arithmetic obvious
    /// wherever a test reads an amount it did not set.
    /// </summary>
    private const long _DEFAULT_STOCK = 1;

    // Society holds the one per-other-player category in the catalogue: the Enemies panel, whose groups are never rendered as themselves but stamped
    // out once per other Player in the game.
    private const string _META_SOCIETY = "Society";
    private const string _CATEGORY_FOREIGN_POPULATION = "Society.ForeignPopulation";
    private const string _GROUP_FOREIGN_SUBJUGATED = "Society.ForeignPopulation.Subjugated";
    private const string _RESOURCE_FOREIGN_SLAVES = "Society.ForeignPopulation.Subjugated.Slaves";

    private GetPlayerAssetsHandler _handler = null!;
    private IMediator _mediator = null!;

    /// <summary>The bank <see cref="StockEveryBankedAssetAsync"/> creates, which is the only one this fixture uses.</summary>
    private int _bankId;

    [SetUp]
    public async Task CreateHandlerAndStockTheBankAsync()
    {
        _mediator = Substitute.For<IMediator>();
        StubGameContext(playerIsInGame: true);

        // The real quantity reader rather than a substitute: it has its own fixture, and what is pinned here is that a resolved figure reaches the
        // contract, which a stub would assert about itself.
        _handler = new GetPlayerAssetsHandler(
            NullLogger<GetPlayerAssetsHandler>.Instance,
            _mediator,
            DbFactory,
            new PlayerAssetQuantityReader(new AssetBankReader()),
            new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory));

        await StockEveryBankedAssetAsync();
    }

    [Test, Description("A Player who has unlocked nothing gets an empty tree rather than the whole catalogue, since the unlock rows are the only gate on visibility.")]
    public async Task ShouldReturnNothingWhenNoMetaCategoryIsUnlocked()
    {
        IReadOnlyCollection<PlayerAssetMetaCategory> tree = await SendAsync();

        Assert.That(tree, Is.Empty);
    }

    [Test, Description("An unlocked meta-category with nothing available beneath it still appears, with no categories: hiding something the Player has been granted would be stranger than showing it empty.")]
    public async Task ShouldReturnTheMetaCategoryWhenItIsUnlockedButHoldsNothingVisible()
    {
        await GrantAsync([], [_META_EMPTY]);

        PlayerAssetMetaCategory metaCategory = Single(await SendAsync());

        Assert.Multiple(() =>
        {
            Assert.That(metaCategory.Code, Is.EqualTo(_META_EMPTY));
            Assert.That(metaCategory.Categories, Is.Empty);
            Assert.That(metaCategory.IconPath, Is.Not.Empty, "the toolbar icon is the whole point of returning an empty meta-category");
        });
    }

    [Test, Description("An Asset on the grouped arm is nested meta-category to category to group to asset, placed by its AssetGroupId rather than the denormalised meta-category column.")]
    public async Task ShouldNestAGroupedAssetUnderItsGroupAndCategory()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        PlayerAssetCategory category = Single(Single(await SendAsync()).Categories);
        PlayerAssetGroup group = Single(category.Groups);
        PlayerAsset asset = Single(group.Assets);

        Assert.Multiple(() =>
        {
            Assert.That(category.Code, Is.EqualTo(_CATEGORY_WOOD));
            Assert.That(category.CategoryType, Is.EqualTo(AssetCategoryType.GroupedAssets));
            Assert.That(category.Assets, Is.Empty, "a grouped category carries its Assets through its groups, never directly");
            Assert.That(group.Code, Is.EqualTo(_GROUP_LOGS));
            Assert.That(asset.Code, Is.EqualTo(_RESOURCE_OAK_LOG));
        });
    }

    [Test, Description("An Asset on the standalone arm hangs off its category with no group between, and the category reports StandaloneAssets so the client picks the flat layout.")]
    public async Task ShouldPlaceAStandaloneAssetDirectlyOnItsCategory()
    {
        await GrantAsync([_RESOURCE_CODE], [_META_STANDALONE]);

        PlayerAssetCategory category = Single(Single(await SendAsync()).Categories);

        Assert.Multiple(() =>
        {
            Assert.That(category.Code, Is.EqualTo(_CATEGORY_MELEE));
            Assert.That(category.CategoryType, Is.EqualTo(AssetCategoryType.StandaloneAssets));
            Assert.That(category.Groups, Is.Empty);
            Assert.That(Single(category.Assets).Code, Is.EqualTo(_RESOURCE_CODE));
        });
    }

    [Test, Description("ObsoleteInRounds counts forward from the current Round, because ObsoleteOnRound is the Round availability ends rather than a marker that it already has.")]
    public async Task ShouldReportTheRoundsRemainingWhenAAssetIsObsoleting()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED], obsoleteInRounds: 4);

        Assert.That(FirstAsset(await SendAsync()).ObsoleteInRounds, Is.EqualTo(4));
    }

    [Test, Description("An Asset with no scheduled retirement reports a null countdown rather than zero, which a client would read as obsoleting this Round.")]
    public async Task ShouldReportNoCountdownWhenAAssetIsNotObsoleting()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        Assert.That(FirstAsset(await SendAsync()).ObsoleteInRounds, Is.Null);
    }

    [Test, Description("An Asset whose obsoleting Round has passed drops out entirely, so a stale unlock row cannot keep a retired Asset on the panel.")]
    public async Task ShouldHideTheAssetWhenItsObsoletingRoundHasPassed()
    {
        await GrantAsync([], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("The Round availability ends is exclusive, so an Asset is gone on the Round named rather than surviving it.")]
    public async Task ShouldHideTheAssetWhenItsObsoletingRoundIsTheCurrentRound()
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

    [Test, Description("An Asset unlocked beneath a meta-category the Player has not unlocked stays hidden: the meta-category is the outer gate, not the Asset.")]
    public async Task ShouldHideAssetsBeneathAMetaCategoryThatIsNotUnlocked()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_STANDALONE]);

        Assert.That(Single(await SendAsync()).Code, Is.EqualTo(_META_STANDALONE), "only the unlocked meta-category is returned");
    }

    [Test, Description("A group has no unlock row of its own, so it goes away with the last available Asset in it rather than showing an empty row.")]
    public async Task ShouldDropTheGroupWhenObsolescenceEmptiesIt()
    {
        await GrantAsync([_RESOURCE_OAK_PLANK], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG], [], obsoleteInRounds: -1);

        PlayerAssetCategory category = Single(Single(await SendAsync()).Categories);

        Assert.That(Single(category.Groups).Code, Is.EqualTo(_GROUP_PLANKS), "the Logs group held only the obsoleted Asset");
    }

    [Test, Description("A grouped category is visible only while it holds a visible group, so emptying its last group removes the category too.")]
    public async Task ShouldDropTheCategoryWhenObsolescenceEmptiesItsOnlyGroup()
    {
        await GrantAsync([], [_META_GROUPED]);
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("A standalone category is visible only while it holds a visible Asset, which is the same rule reached by the other arm of the catalogue's exclusive-or.")]
    public async Task ShouldDropTheStandaloneCategoryWhenObsolescenceEmptiesIt()
    {
        await GrantAsync([], [_META_STANDALONE]);
        await GrantAsync([_RESOURCE_CODE], [], obsoleteInRounds: -1);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("Every level comes back in SortOrder, since the client renders the collections in the order given and has no ordering of its own.")]
    public async Task ShouldOrderMetaCategoriesCategoriesAndAssetsBySortOrder()
    {
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG, _RESOURCE_CODE], [_META_GROUPED, _META_STANDALONE]);

        IReadOnlyCollection<PlayerAssetMetaCategory> tree = await SendAsync();
        PlayerAsset[] logs = [.. Single(Single(tree.First().Categories).Groups).Assets];

        Assert.Multiple(() =>
        {
            Assert.That(tree.Select(m => m.SortOrder), Is.Ordered);
            Assert.That(tree.SelectMany(m => m.Categories).Select(c => c.SortOrder), Is.Ordered);
            Assert.That(logs.Select(r => r.SortOrder), Is.Ordered);
        });
    }

    [Test, Description("AssetGroup.SortOrder is nullable, and an unnumbered group sorts last by name - a plain ascending sort would put it first, the opposite of the documented intent.")]
    public async Task ShouldSortAnUnnumberedGroupAfterTheNumberedOnes()
    {
        // The seed always numbers a group, so the null has to be written in by hand.
        await using (AscendantEmpireContext arrange = await DbFactory.CreateDbContextAsync())
        {
            AssetGroup logs = await arrange.AssetGroups.SingleAsync(g => g.Code == _GROUP_LOGS);
            logs.SortOrder = null;
            await arrange.SaveChangesAsync();
        }

        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_OAK_PLANK], [_META_GROUPED]);

        PlayerAssetCategory category = Single(Single(await SendAsync()).Categories);

        Assert.That(category.Groups.Select(g => g.Code), Is.EqualTo(new[] { _GROUP_PLANKS, _GROUP_LOGS }));
    }

    [Test, Description("The six unsourced fields come back as zeros and falses, so a client wiring against the real shape cannot mistake a placeholder for measured data.")]
    public async Task ShouldReturnEveryPlaceholderFieldAtItsUnsourcedDefault()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        PlayerAsset asset = FirstAsset(await SendAsync());

        Assert.Multiple(() =>
        {
            // Amount is not among them: it is measured, and has its own cases under the quantity gate below.
            Assert.That(asset.AmountAddedPerTurn, Is.Zero);
            Assert.That(asset.HasStrategicReserve, Is.False);
            Assert.That(asset.IsImported, Is.False);
            Assert.That(asset.IsExported, Is.False);
            Assert.That(asset.DemandNotMet, Is.False);
            Assert.That(asset.IsLuxury, Is.False);
        });
    }

    [Test, Description("A request naming a Player who is not in an in-progress Game is refused rather than answered from whatever unlock rows that Player id happens to have.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGame()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);
        StubGameContext(playerIsInGame: false);

        ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>> result = await HandleAsync();

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Without a current Round the countdowns cannot be computed, so the request fails rather than defaulting to a Round that would silently hide every obsoleting Asset.")]
    public async Task ShouldReturnNotFoundWhenTheGameHasNoCampaignQueue()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        await using (AscendantEmpireContext arrange = await DbFactory.CreateDbContextAsync())
        {
            arrange.TurnQueues.RemoveRange(arrange.TurnQueues);
            await arrange.SaveChangesAsync();
        }

        ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>> result = await HandleAsync();

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // The per-other-player projection
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("The per-other-player category is shown once under its own catalogue name, so the opponents sit inside one Enemies panel rather than each getting a panel of their own.")]
    public async Task ShouldReturnTheForeignCategoryOnceUnderItsOwnName()
    {
        await AddOtherPlayersAsync("The Vashti", "The Keldar");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        PlayerAssetCategory category = Single(Single(await SendAsync()).Categories);

        Assert.Multiple(() =>
        {
            Assert.That(category.Code, Is.EqualTo(_CATEGORY_FOREIGN_POPULATION));
            Assert.That(category.DisplayName, Is.EqualTo("Enemies"));
        });
    }

    [Test, Description("A per-other-player group is stamped out once per other Player, each copy labelled with that Player's people name rather than the template's own.")]
    public async Task ShouldProjectTheForeignTemplateGroupOncePerOtherPlayer()
    {
        await AddOtherPlayersAsync("The Vashti", "The Keldar");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        IReadOnlyCollection<PlayerAssetGroup> groups = Single(Single(await SendAsync()).Categories).Groups;

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(group => group.DisplayName), Is.EqualTo(new[] { "The Keldar", "The Vashti" }),
                "the projections are ordered by people name, so the panel reads alphabetically");
            Assert.That(groups.Select(group => group.Code),
                Is.All.StartsWith(_GROUP_FOREIGN_SUBJUGATED + "."),
                "a projected code extends the template's, so it stays greppable back to the row it came from");
            Assert.That(groups.Select(group => group.SubjectPlayerId), Is.All.Not.Null);
        });
    }

    [Test, Description("A projected group names its subject Player, since the people name is a label and the id is the identity a consumer keys on.")]
    public async Task ShouldCarryTheSubjectPlayerIdOnEachProjectedGroup()
    {
        int[] otherPlayerIds = await AddOtherPlayersAsync("The Vashti");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        PlayerAssetGroup group = Single(Single(Single(await SendAsync()).Categories).Groups);

        Assert.Multiple(() =>
        {
            Assert.That(group.SubjectPlayerId, Is.EqualTo(otherPlayerIds[0]));
            Assert.That(group.Code, Is.EqualTo($"{_GROUP_FOREIGN_SUBJUGATED}.P{otherPlayerIds[0]}"));
            Assert.That(Single(group.Assets).Code, Is.EqualTo(_RESOURCE_FOREIGN_SLAVES));
        });
    }

    [Test, Description("Two projections of one template group carry the same catalogue Id, which is why Code rather than Id is what identifies a group in the returned tree.")]
    public async Task ShouldRepeatTheTemplateGroupIdAcrossProjectionsAndDistinguishThemByCode()
    {
        await AddOtherPlayersAsync("The Vashti", "The Keldar");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        IReadOnlyCollection<PlayerAssetGroup> groups = Single(Single(await SendAsync()).Categories).Groups;

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(group => group.Id).Distinct().Count(), Is.EqualTo(1));
            Assert.That(groups.Select(group => group.Code).Distinct().Count(), Is.EqualTo(groups.Count));
        });
    }

    [Test, Description("The template group is never rendered as itself, so a Player alone in their game sees no Enemies category at all rather than one holding an unlabelled column.")]
    public async Task ShouldReturnNoForeignCategoryWhenThePlayerIsTheOnlyOneInTheGame()
    {
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("A Player in another Game is not a subject here, so the projection cannot leak a people name across games.")]
    public async Task ShouldIgnorePlayersBelongingToAnotherGame()
    {
        await AddOtherPlayersAsync("The Vashti");
        await AddOtherPlayersAsync(gameId: _GAME_ID + 1, peopleNames: "The Outsiders");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        PlayerAssetGroup group = Single(Single(Single(await SendAsync()).Categories).Groups);

        Assert.That(group.DisplayName, Is.EqualTo("The Vashti"));
    }

    [Test, Description("An eliminated Player keeps their column, because people held from them are still held - losing it would leave the holder owning them with no way to see them.")]
    public async Task ShouldStillProjectAPlayerWhoHasBeenEliminated()
    {
        await AddOtherPlayersAsync("The Vashti");
        await AddOtherPlayersAsync(_GAME_ID, PlayerStatus.Eliminated, "The Fallen");
        await GrantAsync([_RESOURCE_FOREIGN_SLAVES], [_META_SOCIETY]);

        IReadOnlyCollection<PlayerAssetGroup> groups = Single(Single(await SendAsync()).Categories).Groups;

        Assert.That(groups.Select(group => group.DisplayName), Is.EqualTo(new[] { "The Fallen", "The Vashti" }));
    }

    [Test, Description("The template group is pruned on its own contents before the fan-out, so a Player who has unlocked none of the foreign Assets gets no Enemies category rather than one empty column per opponent.")]
    public async Task ShouldReturnNoForeignCategoryWhenNothingBeneathTheTemplateIsUnlocked()
    {
        await AddOtherPlayersAsync("The Vashti", "The Keldar");
        await GrantAsync([], [_META_SOCIETY]);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("An ordinary group reports no subject Player, so a consumer can tell a projection from a plain group without consulting the catalogue.")]
    public async Task ShouldLeaveTheSubjectPlayerNullOnAnOrdinaryGroup()
    {
        await GrantAsync([_RESOURCE_OAK_LOG], [_META_GROUPED]);

        Assert.That(Single(Single(Single(await SendAsync()).Categories).Groups).SubjectPlayerId, Is.Null);
    }

    /// <summary>
    /// Adds other Players to the fixture's Game, so the per-other-player template has something to project onto.
    /// </summary>
    /// <returns>The ids of the Players added, in the order their names were given.</returns>
    private Task<int[]> AddOtherPlayersAsync(params string[] peopleNames)
    {
        return AddOtherPlayersAsync(_GAME_ID, PlayerStatus.Active, peopleNames);
    }

    private Task<int[]> AddOtherPlayersAsync(int gameId, params string[] peopleNames)
    {
        return AddOtherPlayersAsync(gameId, PlayerStatus.Active, peopleNames);
    }

    private async Task<int[]> AddOtherPlayersAsync(int gameId, PlayerStatus status, params string[] peopleNames)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player[] players =
        [
            .. peopleNames.Select(peopleName => new Player
            {
                GameId = gameId,
                RulerTitle = Player.DEFAULT_RULER_TITLE,
                RulerName = $"Ruler of {peopleName}",
                FactionName = $"Faction of {peopleName}",
                PeopleName = peopleName,
                Demonym = peopleName,
                CultureName = $"Culture of {peopleName}",
                Language = $"Tongue of {peopleName}",
                RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
                Status = status
            })
        ];

        context.Players.AddRange(players);
        await context.SaveChangesAsync();

        return [.. players.Select(player => player.Id)];
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // The quantity gate
    //
    // An unlock is necessary but no longer sufficient: a banked Asset also has to be one the Player holds some of. The two other storage scopes are
    // exempt, for reasons that differ - see GetPlayerAssetsHandler.EarnsATicker.
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("A banked Asset carries what the Player holds of it, so a ticker shows that stock.")]
    public async Task ShouldReportTheBankedAmountOnTheTicker()
    {
        await GrantAsync();
        await SetBankedAmountAsync(_RESOURCE_CODE, wholeQuantity: 12);

        IReadOnlyCollection<PlayerAssetMetaCategory> tree = await SendAsync();

        Assert.That(FindAsset(tree, _RESOURCE_CODE).Amount, Is.EqualTo(12));
    }

    [Test, Description("A banked Asset the Player holds none of leaves the panel rather than reading zero, which is what keeps the toolbar to what they actually hold.")]
    public async Task ShouldDropABankedAssetThePlayerHoldsNoneOf()
    {
        await GrantAsync();
        await SetBankedAmountAsync(_RESOURCE_CODE, wholeQuantity: 0);

        IReadOnlyCollection<PlayerAssetMetaCategory> tree = await SendAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Single(tree).Code, Is.EqualTo(_META_CATEGORY_CODE), "the meta-category keeps its toolbar icon, which its own unlock row decides");
            Assert.That(Single(tree).Categories, Is.Empty, "the sword was the only Asset beneath it, so its category goes with it");
        });
    }

    [Test, Description("A fraction of a unit is gated as none, matching the figure reported: a drawdown would not release it either.")]
    public async Task ShouldDropABankedAssetHeldOnlyAsAFraction()
    {
        await GrantAsync();

        // Nine tenths of one sword, which floors to none.
        await SetBankedRawAmountAsync(_RESOURCE_CODE, rawQuantity: ScaledAmount.FromWhole(1).RawValue - (ScaledAmount.Scale / 10));

        Assert.That(Single(await SendAsync()).Categories, Is.Empty);
    }

    [Test, Description("A Player-scope Asset stays on the panel at zero: the research points empty at the end of every Turn by design, so a ticker that went with them would come and go.")]
    public async Task ShouldKeepAPlayerScopedAssetWhoseCounterIsEmpty()
    {
        await GrantAsync([_RESOURCE_TECHNOLOGY_EXPERIENCE], [_META_SOCIETY]);

        // No research state is seeded, so the counter behind it reads nothing at all.
        Assert.That(FindAsset(await SendAsync(), _RESOURCE_TECHNOLOGY_EXPERIENCE).Amount, Is.Zero);
    }

    [Test, Description("A Derived Asset stays on the panel at zero, because that zero is a missing ledger rather than a measurement - hiding Civilians would state something false.")]
    public async Task ShouldKeepADerivedAssetThatNothingRecordsYet()
    {
        await GrantAsync([_RESOURCE_CIVILIANS], [_META_SOCIETY]);

        Assert.That(FindAsset(await SendAsync(), _RESOURCE_CIVILIANS).Amount, Is.Zero);
    }

    [Test, Description("The gate prunes upwards the same way obsolescence does, so a group whose Assets are all held at none takes its category away with it.")]
    public async Task ShouldDropTheGroupAndItsCategoryWhenTheGateEmptiesThem()
    {
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG], [_META_GROUPED]);
        await SetBankedAmountAsync(_RESOURCE_OAK_LOG, wholeQuantity: 0);
        await SetBankedAmountAsync(_RESOURCE_CEDAR_LOG, wholeQuantity: 0);

        Assert.That(Single(await SendAsync()).Categories, Is.Empty, "the Logs group held both, and the Wood category held only that group");
    }

    [Test, Description("The gate is per Asset rather than per group, so a group keeps the Assets held and loses only the ones held at none.")]
    public async Task ShouldKeepTheGroupWhenOnlySomeOfItsAssetsAreHeldAtNone()
    {
        await GrantAsync([_RESOURCE_OAK_LOG, _RESOURCE_CEDAR_LOG], [_META_GROUPED]);
        await SetBankedAmountAsync(_RESOURCE_CEDAR_LOG, wholeQuantity: 0);

        PlayerAssetGroup group = Single(Single(Single(await SendAsync()).Categories).Groups);

        Assert.That(Single(group.Assets).Code, Is.EqualTo(_RESOURCE_OAK_LOG));
    }

    /// <summary>
    /// Runs the handler and asserts it succeeded, returning the tree.
    /// </summary>
    private async Task<IReadOnlyCollection<PlayerAssetMetaCategory>> SendAsync()
    {
        ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>> result = await HandleAsync();

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    private Task<ServiceResult<IReadOnlyCollection<PlayerAssetMetaCategory>>> HandleAsync()
    {
        return _handler.Handle(new GetPlayerAssetsRequest(_GAME_ID, _playerId), CancellationToken.None);
    }

    /// <summary>
    /// Gives the fixture Player one bank holding <see cref="_DEFAULT_STOCK"/> of every banked Asset in the catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nearly every case here is about the <b>shape</b> of the tree - placement, ordering, obsolescence, the per-other-player fan-out - and the handler
    /// now drops a banked Asset the Player holds none of. Without a stocked bank that gate would quietly remove the subject of those cases, and they
    /// would stop proving what they claim to. A case that is about the gate sets its own figure with <see cref="SetBankedAmountAsync"/>.
    /// </para>
    /// <para>
    /// No Settlement row is written and no membership points at the bank. The bank's PlayerId is what the read follows, and a membership would only say
    /// which Settlements can reach it - which no figure on this contract depends on.
    /// </para>
    /// </remarks>
    private async Task StockEveryBankedAssetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var bank = new AssetBank { PlayerId = _playerId };
        context.AssetBanks.Add(bank);
        await context.SaveChangesAsync();

        _bankId = bank.Id;

        // Only the banked scope. A balance row against a Player-scope or Derived Asset would be data production never writes, and the reader would not
        // consult it anyway.
        int[] bankedAssetIds = await context.Assets
            .Where(asset => asset.StorageScope == AssetStorageScope.SettlementBank)
            .Select(asset => asset.Id)
            .ToArrayAsync();

        context.AssetBankBalances.AddRange(bankedAssetIds.Select(assetId => new AssetBankBalance
        {
            AssetBankId = _bankId,
            AssetId = assetId,
            Quantity = ScaledAmount.FromWhole(_DEFAULT_STOCK).RawValue
        }));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Sets what the fixture Player holds of one banked Asset, in whole units.
    /// </summary>
    private Task SetBankedAmountAsync(string assetCode, long wholeQuantity)
    {
        return SetBankedRawAmountAsync(assetCode, ScaledAmount.FromWhole(wholeQuantity).RawValue);
    }

    /// <summary>
    /// Sets what the fixture Player holds of one banked Asset, in raw scaled units, for the cases that turn on a fraction.
    /// </summary>
    /// <remarks>
    /// Rewrites the balance on the one bank <see cref="StockEveryBankedAssetAsync"/> created rather than adding a second bank, so the figure a case sets
    /// is the figure the handler reports rather than that figure plus the fixture's stock. Summing across banks is pinned in
    /// <c>PlayerAssetQuantityReaderTests</c>, where it belongs.
    /// </remarks>
    private async Task SetBankedRawAmountAsync(string assetCode, long rawQuantity)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int assetId = await context.Assets.Where(asset => asset.Code == assetCode).Select(asset => asset.Id).SingleAsync();

        AssetBankBalance balance = await context.AssetBankBalances
            .SingleAsync(row => row.AssetBankId == _bankId && row.AssetId == assetId);

        balance.Quantity = rawQuantity;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Finds the one Asset in the tree with the given code, down either placement path.
    /// </summary>
    private static PlayerAsset FindAsset(IReadOnlyCollection<PlayerAssetMetaCategory> tree, string assetCode)
    {
        PlayerAsset[] matches =
        [
            .. tree
                .SelectMany(metaCategory => metaCategory.Categories)
                .SelectMany(category => category.Assets.Concat(category.Groups.SelectMany(group => group.Assets)))
                .Where(asset => asset.Code == assetCode)
        ];

        Assert.That(matches, Has.Length.EqualTo(1), () => $"the tree holds {matches.Length} Assets with code {assetCode}");

        return matches[0];
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
    /// The first Asset in the tree, whichever arm of the catalogue it arrived by.
    /// </summary>
    private static PlayerAsset FirstAsset(IReadOnlyCollection<PlayerAssetMetaCategory> tree)
    {
        PlayerAsset[] assets =
        [
            .. tree
                .SelectMany(metaCategory => metaCategory.Categories)
                .SelectMany(category => category.Assets.Concat(category.Groups.SelectMany(group => group.Assets)))
        ];

        Assert.That(assets, Is.Not.Empty);

        return assets.First();
    }
}
