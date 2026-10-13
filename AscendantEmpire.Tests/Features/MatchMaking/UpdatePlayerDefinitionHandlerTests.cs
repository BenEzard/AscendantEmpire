using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for saving the ruler and faction identity a user chooses for the slot they control.
/// </summary>
/// <remarks>
/// <para>
/// The handler resolves the slot from the game and the token's user id rather than from a player id in the request, so "someone else's slot" and "no
/// slot at all" collapse into the same case and both answer 404. That is the property most worth pinning here, because it is what makes the endpoint
/// safe to reach while the caller still holds a plain user token.
/// </para>
/// <para>
/// Tier 3, where this fixture used to be in memory: the save and the game start it may trigger share an explicit transaction, and
/// <c>BeginTransactionAsync</c> throws on the in-memory provider. The round service is substituted rather than real - the start <i>seam</i> is
/// <c>StartOfGameTests</c>' job, and what matters here is only whether this handler reaches for it.
/// </para>
/// <para>
/// The six uniqueness rules are checked by the handler as well as by indexes, so the refusals below are the handler's own. The indexes behind them are
/// <see cref="PlayerIdentityIndexTests"/>' subject.
/// </para>
/// </remarks>
[TestFixture]
internal class UpdatePlayerDefinitionHandlerTests : SqlServerTestBase
{
    /// <summary>The colour the rival already holds, stored the way the handler stores it: lower-cased.</summary>
    private const string _RIVAL_COLOUR = "#c10007";

    /// <summary>The portrait the rival already holds, so the caller may not take it.</summary>
    private const string _RIVAL_PORTRAIT = "/portraits/ruler_04.png";

    private ICampaignRoundService _campaignRoundService = null!;
    private UpdatePlayerDefinitionHandler _handler = null!;

    private int _userId;
    private int _rivalUserId;
    private int _gameId;
    private int _otherGameId;
    private int _ownPlayerId;
    private int _rivalPlayerId;

    [SetUp]
    public async Task SeedPlayersAsync()
    {
        _campaignRoundService = Substitute.For<ICampaignRoundService>();
        _campaignRoundService
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.Succeed());

        _handler = new UpdatePlayerDefinitionHandler(NullLogger<UpdatePlayerDefinitionHandler>.Instance, DbFactory, _campaignRoundService);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _userId = await AddUserAsync(context, "definer");
        _rivalUserId = await AddUserAsync(context, "rival");

        GameIndex game = BuildGame("Definition Game", GamePlayStatus.AwaitingPlayers);
        GameIndex otherGame = BuildGame("Unrelated Game", GamePlayStatus.AwaitingPlayers);

        context.GameIndex.AddRange(game, otherGame);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _otherGameId = otherGame.Id;

        // The caller's own slot, still undefined - a seeded slot's gender is null.
        Player ownSlot = BuildSlot(_gameId, _userId, suffix: 1, campaignOffsetId: 1);

        // A rival in the same game who has already defined themselves, so their values are taken.
        Player rivalSlot = BuildSlot(_gameId, _rivalUserId, suffix: 2, campaignOffsetId: 2);
        rivalSlot.FactionColour = _RIVAL_COLOUR;
        rivalSlot.RulerPortraitPath = _RIVAL_PORTRAIT;
        rivalSlot.RulerGender = RulerGender.Female;

        // A slot in a different game holding the same colour and portrait, and names of its own, so that "already taken
        // somewhere else" is distinguishable from "already taken here".
        Player elsewhere = BuildSlot(_otherGameId, _rivalUserId, suffix: 9, campaignOffsetId: 3);
        elsewhere.FactionColour = _RIVAL_COLOUR;
        elsewhere.RulerPortraitPath = _RIVAL_PORTRAIT;

        context.Players.AddRange(ownSlot, rivalSlot, elsewhere);
        await context.SaveChangesAsync();

        _ownPlayerId = ownSlot.Id;
        _rivalPlayerId = rivalSlot.Id;
    }

    // =========================================================================
    // Saving a definition
    // =========================================================================

    [Test, Description("A save writes all ten values to the caller's own slot, storing the colour lower-cased so a later clash check cannot miss it.")]
    public async Task SaveWritesEveryValueToTheCallersOwnSlotAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(
                rulerTitle: "  Empress  ",
                rulerName: "  Aelara  ",
                factionName: "  The Ember Pact  ",
                factionColour: "#C800DE",
                peopleName: "  The Vashti  ",
                demonym: "  Vashti  ",
                cultureName: "  Vashti Highland  ",
                language: "  Old Vashti  ",
                rulerPortraitPath: "/portraits/ruler_12.png"),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.RulerTitle, Is.EqualTo("Empress"), "Surrounding whitespace is trimmed off.");
            Assert.That(saved.RulerName, Is.EqualTo("Aelara"));
            Assert.That(saved.RulerGender, Is.EqualTo(RulerGender.Female));
            Assert.That(saved.FactionName, Is.EqualTo("The Ember Pact"));
            Assert.That(saved.FactionColour, Is.EqualTo("#c800de"), "Colours are stored lower-cased, so a clash is detectable whatever the database collation.");
            Assert.That(saved.PeopleName, Is.EqualTo("The Vashti"));
            Assert.That(saved.Demonym, Is.EqualTo("Vashti"));
            Assert.That(saved.CultureName, Is.EqualTo("Vashti Highland"));
            Assert.That(saved.Language, Is.EqualTo("Old Vashti"));
            Assert.That(saved.RulerPortraitPath, Is.EqualTo("/portraits/ruler_12.png"), "The portrait the player picked, not the one their slot was seeded with.");
        }
    }

    [Test, Description("A blank ruler title is a valid choice and lands as the default placeholder, not as an empty string.")]
    public async Task BlankRulerTitleIsStoredAsTheDefaultTitleAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(rulerTitle: "   "), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.RulerTitle, Is.EqualTo(Player.DEFAULT_RULER_TITLE));
    }

    [Test, Description("A caller holding no slot in the game gets a 404, because the request names no player there is anything to forbid them from.")]
    public async Task CallerWithoutASlotInTheGameIsNotFoundAsync()
    {
        // The caller holds nothing in _otherGameId, where only the rival has a slot.
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(gameId: _otherGameId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    // =========================================================================
    // Whitespace normalisation
    // =========================================================================

    [Test, Description("Runs of internal whitespace in the five unique names are collapsed to single spaces, so a double space cannot stand in for a second name.")]
    public async Task InternalWhitespaceInTheUniqueNamesIsCollapsedAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(
                rulerName: "Ael  ara",
                factionName: "The   Ember\tPact",
                peopleName: "The  Vashti",
                demonym: "Vash  ti",
                cultureName: "Vashti\t\tHighland"),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.RulerName, Is.EqualTo("Ael ara"));
            Assert.That(saved.FactionName, Is.EqualTo("The Ember Pact"));
            Assert.That(saved.PeopleName, Is.EqualTo("The Vashti"));
            Assert.That(saved.Demonym, Is.EqualTo("Vash ti"));
            Assert.That(saved.CultureName, Is.EqualTo("Vashti Highland"));
        }
    }

    [Test, Description("A name whose only difference from a rival's is extra internal spacing is a clash, because the stored form of both is the same.")]
    public async Task NameDifferingOnlyByInternalWhitespaceIsRejectedAsync()
    {
        await DefineRivalAsync(peopleName: "The Vashti");

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(peopleName: "The   Vashti"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Does.Contain("peopleName"));
        }
    }

    // =========================================================================
    // Uniqueness within the game
    // =========================================================================

    [TestCase("rulerName", Description = "A ruler name another player in the game already holds is rejected before the save.")]
    [TestCase("factionName", Description = "A faction name another player in the game already holds is rejected before the save.")]
    [TestCase("peopleName", Description = "A people name another player in the game already holds is rejected, where it used to be shareable flavour text.")]
    [TestCase("demonym", Description = "A demonym another player in the game already holds is rejected before the save.")]
    [TestCase("cultureName", Description = "A culture name another player in the game already holds is rejected before the save.")]
    public async Task NameHeldByAnotherPlayerInTheGameIsRejectedAsync(string field)
    {
        const string taken = "Vashti";

        await DefineRivalAsync(
            rulerName: field == "rulerName" ? taken : "Rival Ruler",
            factionName: field == "factionName" ? taken : "Rival Faction",
            peopleName: field == "peopleName" ? taken : "Rival People",
            demonym: field == "demonym" ? taken : "Rival Person",
            cultureName: field == "cultureName" ? taken : "Rival Culture");

        UpdatePlayerDefinitionRequest request = BuildRequest(
            rulerName: field == "rulerName" ? taken : "Aelara",
            factionName: field == "factionName" ? taken : "The Ember Pact",
            peopleName: field == "peopleName" ? taken : "The Vashti",
            demonym: field == "demonym" ? taken : "Vashti One",
            cultureName: field == "cultureName" ? taken : "Vashti Highland");

        ServiceResult<Empty> result = await _handler.Handle(request, CancellationToken.None);

        Player unchanged = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Is.EqualTo(new[] { field }));
            Assert.That(unchanged.RulerGender, Is.Null, "A rejected save leaves the slot exactly as it was, still undefined.");
        }
    }

    [Test, Description("Names are compared case-insensitively, so the same name in different capitals is one name rather than two.")]
    public async Task NameDifferingOnlyInCaseIsRejectedAsync()
    {
        await DefineRivalAsync(peopleName: "The Vashti");

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(peopleName: "the vashti"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Does.Contain("peopleName"));
        }
    }

    [Test, Description("Every clash is reported in one answer, so a player who picks three taken values is told all three rather than resubmitting three times.")]
    public async Task EveryClashIsReportedTogetherAsync()
    {
        await DefineRivalAsync(peopleName: "The Vashti", demonym: "Vashti", cultureName: "Vashti Highland");

        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(peopleName: "The Vashti", demonym: "Vashti", cultureName: "Vashti Highland"),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Is.EquivalentTo(new[] { "peopleName", "demonym", "cultureName" }));
            Assert.That(result.Errors, Has.Count.EqualTo(3), "One message per clash, so every one of them can be shown.");
        }
    }

    [Test, Description("Two players sharing a language is allowed, because it is the one identity name deliberately left out of the uniqueness rule.")]
    public async Task LanguageHeldByAnotherPlayerIsAcceptedAsync()
    {
        await DefineRivalAsync(language: "Old Vashti");

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(language: "Old Vashti"), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("Re-saving the values the caller already holds is not a clash with themselves.")]
    public async Task KeepingTheCallersOwnValuesIsAcceptedAsync()
    {
        ServiceResult<Empty> first = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(first.HasSucceeded, Is.True, () => string.Join("; ", first.Errors));

        ServiceResult<Empty> second = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(second.HasSucceeded, Is.True, () => string.Join("; ", second.Errors));
    }

    [Test, Description("Names are unique per game rather than globally, so one already used in a different game is still available here.")]
    public async Task NameUsedInADifferentGameIsStillAvailableAsync()
    {
        // The slot in _otherGameId carries the "People 9" placeholder; taking it here only works if the rule is scoped by game.
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(peopleName: "People 9"), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [TestCase("#c10007", Description = "A colour another player in the game already holds is rejected before the save.")]
    [TestCase("#C10007", Description = "The clash check ignores case, so the same colour in capitals is rejected too.")]
    public async Task ColourHeldByAnotherPlayerInTheGameIsRejectedAsync(string requestedColour)
    {
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(factionColour: requestedColour), CancellationToken.None);

        Player unchanged = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Is.EqualTo(new[] { "factionColour" }));
            Assert.That(unchanged.FactionColour, Is.Null, "A rejected save leaves the slot exactly as it was.");
        }
    }

    [Test, Description("Colours are unique per game rather than globally, so one already used in a different game is still available here.")]
    public async Task ColourUsedInADifferentGameIsStillAvailableAsync()
    {
        // Free the colour up within this game; the slot in _otherGameId keeps holding it.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player rival = await context.Players.SingleAsync(p => p.Id == _rivalPlayerId);
            rival.FactionColour = null;
            await context.SaveChangesAsync();
        }

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(factionColour: _RIVAL_COLOUR), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.FactionColour, Is.EqualTo(_RIVAL_COLOUR));
    }

    [Test, Description("A portrait a rival in the same game already wears is refused, so no two players share a face.")]
    public async Task PortraitHeldByARivalIsRejectedAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(rulerPortraitPath: _RIVAL_PORTRAIT), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(ClashingFields(result), Is.EqualTo(new[] { "rulerPortraitPath" }));
        }
    }

    [Test, Description("The same portrait held in a different game is no obstacle, since the rule is per game rather than global.")]
    public async Task PortraitHeldInAnotherGameIsAcceptedAsync()
    {
        // The slot in _otherGameId wears _RIVAL_PORTRAIT too, so this only passes if the check is scoped by game.
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(rulerPortraitPath: "/portraits/ruler_09.png"), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("A blank portrait lands as the default rather than being refused as a clash with whoever else has none.")]
    public async Task BlankPortraitIsStoredAsTheDefaultAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(rulerPortraitPath: "   "), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.RulerPortraitPath, Is.EqualTo(Player.DEFAULT_RULER_PORTRAIT_PATH));
    }

    [Test, Description("The default is exempt from the uniqueness rule, so any number of players may be undecided at once.")]
    public async Task DefaultPortraitIsAcceptedWhileARivalAlsoHasNoneAsync()
    {
        // Put the rival on the default as well, so the only thing that can let this through is the exemption.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player rival = await context.Players.SingleAsync(p => p.Id == _rivalPlayerId);
            rival.RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH;
            await context.SaveChangesAsync();
        }

        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(rulerPortraitPath: Player.DEFAULT_RULER_PORTRAIT_PATH),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    // =========================================================================
    // The lock once play has begun
    // =========================================================================

    [Test, Description("Once the game is in progress a save writes the ruler title and nothing else, so the identity everything labels by cannot move under it.")]
    public async Task LockedGameAppliesTheTitleAndIgnoresEverythingElseAsync()
    {
        await DefineOwnSlotAsync();
        await SetPlayStatusAsync(GamePlayStatus.InProgress);

        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(
                rulerTitle: "Dowager Empress",
                rulerName: "Someone Else",
                rulerGender: RulerGender.Male,
                factionName: "A New Pact",
                factionColour: "#010203",
                peopleName: "A New People",
                demonym: "Newcomer",
                cultureName: "A New Culture",
                language: "A New Tongue",
                rulerPortraitPath: "/portraits/ruler_18.png"),
            CancellationToken.None);

        Player saved = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(saved.RulerTitle, Is.EqualTo("Dowager Empress"), "The title is the one field that stays editable for the life of the game.");
            Assert.That(saved.RulerName, Is.EqualTo("Aelara"));
            Assert.That(saved.RulerGender, Is.EqualTo(RulerGender.Female));
            Assert.That(saved.FactionName, Is.EqualTo("The Ember Pact"));
            Assert.That(saved.FactionColour, Is.EqualTo("#c800de"));
            Assert.That(saved.PeopleName, Is.EqualTo("The Vashti"));
            Assert.That(saved.Demonym, Is.EqualTo("Vashti"));
            Assert.That(saved.CultureName, Is.EqualTo("Vashti Highland"));
            Assert.That(saved.Language, Is.EqualTo("Old Vashti"));
            Assert.That(saved.RulerPortraitPath, Is.EqualTo("/portraits/ruler_07.png"));
        }
    }

    [Test, Description("Clearing the title mid-game is a legal edit and lands as the default placeholder, exactly as it does before play begins.")]
    public async Task LockedGameStillDefaultsABlankTitleAsync()
    {
        await DefineOwnSlotAsync();
        await SetPlayStatusAsync(GamePlayStatus.InProgress);

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(rulerTitle: "   "), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.RulerTitle, Is.EqualTo(Player.DEFAULT_RULER_TITLE));
    }

    [Test, Description("A locked save is not refused over a value a rival holds, because those fields are not read at all - a title edit must not fail on what the client echoed back.")]
    public async Task LockedGameDoesNotReportClashesAsync()
    {
        await DefineOwnSlotAsync();
        await SetPlayStatusAsync(GamePlayStatus.InProgress);

        ServiceResult<Empty> result = await _handler.Handle(
            BuildRequest(rulerTitle: "Dowager Empress", factionColour: _RIVAL_COLOUR, rulerPortraitPath: _RIVAL_PORTRAIT),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [TestCase(GamePlayStatus.Completed, Description = "A finished game's definition stays locked, so a result cannot be rewritten after the fact.")]
    [TestCase(GamePlayStatus.Abandoned, Description = "An abandoned game's definition stays locked for the same reason a finished one's does.")]
    public async Task GamesPastInProgressStayLockedAsync(GamePlayStatus playStatus)
    {
        await DefineOwnSlotAsync();
        await SetPlayStatusAsync(playStatus);

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(peopleName: "A New People"), CancellationToken.None);

        Player saved = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(saved.PeopleName, Is.EqualTo("The Vashti"));
        }
    }

    [Test, Description("A locked save never reaches for the game start, because a game past the lobby has already begun.")]
    public async Task LockedGameDoesNotTryToStartAsync()
    {
        await DefineOwnSlotAsync();
        await SetPlayStatusAsync(GamePlayStatus.InProgress);
        _campaignRoundService.ClearReceivedCalls();

        await _handler.Handle(BuildRequest(rulerTitle: "Dowager Empress"), CancellationToken.None);

        await _campaignRoundService.DidNotReceive()
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Starting the game on the last definition
    // =========================================================================

    [Test, Description("The last player to define themselves starts the game, which is what makes the trigger a definition rather than a claim.")]
    public async Task LastDefinitionStartsTheGameAsync()
    {
        // The rival is already claimed and defined, so the caller's own definition is the outstanding one.
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await _campaignRoundService.Received(1)
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>());
    }

    [Test, Description("A definition that still leaves somebody undefined does not start the game, so a seat claimed and walked away from holds play up.")]
    public async Task DefinitionLeavingSomebodyUndefinedDoesNotStartTheGameAsync()
    {
        // Put the rival back to undefined, so the caller is not the last one in.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player rival = await context.Players.SingleAsync(p => p.Id == _rivalPlayerId);
            rival.RulerGender = null;
            await context.SaveChangesAsync();
        }

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await _campaignRoundService.DidNotReceive()
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test, Description("An unclaimed seat holds the game up however many players are defined, because an empty seat has nobody to take its Turn.")]
    public async Task DefinitionWithAnUnclaimedSeatLeftDoesNotStartTheGameAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.Players.Add(BuildSlot(_gameId, userId: null, suffix: 3, campaignOffsetId: 4));
            await context.SaveChangesAsync();
        }

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await _campaignRoundService.DidNotReceive()
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test, Description("An inactive seat is not counted, so a game whose every remaining player is in and defined starts even with a slot nobody ever took.")]
    public async Task InactiveSeatDoesNotHoldTheGameUpAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player abandoned = BuildSlot(_gameId, userId: null, suffix: 3, campaignOffsetId: 4);
            abandoned.Status = PlayerStatus.ExcludedDueToInactivity;
            context.Players.Add(abandoned);
            await context.SaveChangesAsync();
        }

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await _campaignRoundService.Received(1)
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), _gameId, Arg.Any<CancellationToken>());
    }

    [Test, Description("A start that fails rolls the definition back, so re-saving it is a real retry rather than a second failure against a game stuck full.")]
    public async Task FailedStartRollsTheDefinitionBackAsync()
    {
        _campaignRoundService
            .StartGameAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.Fail("The first Round could not be opened."));

        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Player unchanged = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(unchanged.RulerGender, Is.Null, "The slot must still be undefined, so saving again triggers the start afresh.");
            Assert.That(unchanged.FactionColour, Is.Null);
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>A valid request against the caller's own slot, with only the values a test cares about overridden.</summary>
    private UpdatePlayerDefinitionRequest BuildRequest(
        int? userId = null,
        int? gameId = null,
        string? rulerTitle = "Empress",
        string rulerName = "Aelara",
        RulerGender rulerGender = RulerGender.Female,
        string factionName = "The Ember Pact",
        string factionColour = "#c800de",
        string peopleName = "The Vashti",
        string demonym = "Vashti",
        string cultureName = "Vashti Highland",
        string language = "Old Vashti",
        string rulerPortraitPath = "/portraits/ruler_07.png")
    {
        return new UpdatePlayerDefinitionRequest(
            userId ?? _userId,
            gameId ?? _gameId,
            rulerTitle,
            rulerName,
            rulerGender,
            factionName,
            factionColour,
            peopleName,
            demonym,
            cultureName,
            language,
            rulerPortraitPath);
    }

    /// <summary>Saves the caller's own definition through the handler, so a lock test starts from a real one rather than placeholders.</summary>
    private async Task DefineOwnSlotAsync()
    {
        ServiceResult<Empty> result = await _handler.Handle(BuildRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>Overwrites whichever of the rival's values a test needs to have taken. Only the named ones are touched.</summary>
    private async Task DefineRivalAsync(
        string? rulerName = null,
        string? factionName = null,
        string? peopleName = null,
        string? demonym = null,
        string? cultureName = null,
        string? language = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player rival = await context.Players.SingleAsync(p => p.Id == _rivalPlayerId);

        rival.RulerName = rulerName ?? rival.RulerName;
        rival.FactionName = factionName ?? rival.FactionName;
        rival.PeopleName = peopleName ?? rival.PeopleName;
        rival.Demonym = demonym ?? rival.Demonym;
        rival.CultureName = cultureName ?? rival.CultureName;
        rival.Language = language ?? rival.Language;

        await context.SaveChangesAsync();
    }

    private async Task SetPlayStatusAsync(GamePlayStatus playStatus)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.GameIndex
            .Where(g => g.Id == _gameId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.PlayStatus, playStatus));
    }

    private async Task<Player> LoadOwnSlotAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Players.SingleAsync(p => p.Id == _ownPlayerId);
    }

    /// <summary>The fields a refusal named, read off the structured detail the 409 carries.</summary>
    private static IReadOnlyCollection<string> ClashingFields(ServiceResult<Empty> result)
    {
        Assert.That(result.ErrorDetail, Is.TypeOf<PlayerDefinitionConflict>(), "A clash must carry the detail that says which fields it was.");

        return [.. ((PlayerDefinitionConflict)result.ErrorDetail!).Fields.Select(f => f.Field)];
    }

    private static async Task<int> AddUserAsync(AscendantEmpireContext context, string username)
    {
        var user = new User
        {
            Username = username,
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>A minimal game row. The gallery id is a plain int with no foreign key behind it, so nothing else needs seeding.</summary>
    private static GameIndex BuildGame(string gameName, GamePlayStatus playStatus)
    {
        return new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = playStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };
    }

    /// <summary>
    /// A slot carrying the placeholders game setup seeds, suffixed per slot.
    /// </summary>
    /// <remarks>
    /// The suffix is what keeps the five unique names apart within one game, exactly as <c>GameSetupService</c> does it. The portrait stays on the
    /// default, which is the one value the uniqueness rule exempts.
    /// </remarks>
    private static Player BuildSlot(int gameId, int? userId, int suffix, int campaignOffsetId)
    {
        return new Player
        {
            GameId = gameId,
            UserId = userId,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = $"Player {suffix}",
            FactionName = $"Faction {suffix}",
            PeopleName = $"People {suffix}",
            Demonym = $"Person {suffix}",
            CultureName = $"Culture {suffix}",
            Language = $"Language {suffix}",
            RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
            Status = PlayerStatus.Active,
            CampaignOffsetId = campaignOffsetId,
        };
    }
}
