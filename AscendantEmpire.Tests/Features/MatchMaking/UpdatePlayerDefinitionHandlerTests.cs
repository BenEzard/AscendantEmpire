using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
/// Runs at the in-memory tier: both clash checks - the faction colour's and the ruler portrait's - are made by the handler itself, so nothing here
/// depends on the database. <see cref="PlayerFactionColourIndexTests"/> covers the colour's unique index; the portrait has no index behind it, which
/// makes the check below its only guarantee and so the only place the rule is pinned at all.
/// </para>
/// </remarks>
internal class UpdatePlayerDefinitionHandlerTests : InMemoryTestBase
{
    private const int _USER_ID = 11;
    private const int _RIVAL_USER_ID = 12;
    private const int _GAME_ID = 4;
    private const int _OTHER_GAME_ID = 5;

    private const int _OWN_PLAYER_ID = 1;
    private const int _RIVAL_PLAYER_ID = 2;

    /// <summary>The colour the rival already holds, stored the way the handler stores it: lower-cased.</summary>
    private const string _RIVAL_COLOUR = "#c10007";

    /// <summary>The portrait the rival already holds, so the caller may not take it.</summary>
    private const string _RIVAL_PORTRAIT = "/portraits/ruler_04.png";

    private UpdatePlayerDefinitionHandler _handler = null!;

    [SetUp]
    public async Task SeedPlayersAsync()
    {
        _handler = new UpdatePlayerDefinitionHandler(NullLogger<UpdatePlayerDefinitionHandler>.Instance, DbFactory);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // The caller's own slot, still undefined.
        context.Players.Add(new Player
        {
            Id = _OWN_PLAYER_ID,
            GameId = _GAME_ID,
            UserId = _USER_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Player 1",
            FactionName = "Faction 1",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        });

        // A rival in the same game who has already taken a colour.
        context.Players.Add(new Player
        {
            Id = _RIVAL_PLAYER_ID,
            GameId = _GAME_ID,
            UserId = _RIVAL_USER_ID,
            RulerTitle = "Queen",
            RulerName = "Rival",
            FactionName = "Rival Faction",
            FactionColour = _RIVAL_COLOUR,
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = _RIVAL_PORTRAIT,
            RulerGender = RulerGender.Female,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 2,
        });

        // A slot in a different game holding the same colour, which must not collide with this one.
        context.Players.Add(new Player
        {
            Id = 3,
            GameId = _OTHER_GAME_ID,
            UserId = _RIVAL_USER_ID,
            RulerTitle = "King",
            RulerName = "Elsewhere",
            FactionName = "Other Faction",
            FactionColour = _RIVAL_COLOUR,
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = _RIVAL_PORTRAIT,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 3,
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("A save writes all nine values to the caller's own slot, storing the colour lower-cased so a later clash check cannot miss it.")]
    public async Task SaveWritesEveryValueToTheCallersOwnSlotAsync()
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
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
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerTitle: "   "),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.RulerTitle, Is.EqualTo(Player.DEFAULT_RULER_TITLE));
    }

    [Test, Description("A caller holding no slot in the game gets a 404, because the request names no player there is anything to forbid them from.")]
    public async Task CallerWithoutASlotInTheGameIsNotFoundAsync()
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(gameId: _OTHER_GAME_ID),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [TestCase("#c10007", Description = "A colour another player in the game already holds is rejected before the save.")]
    [TestCase("#C10007", Description = "The clash check ignores case, so the same colour in capitals is rejected too.")]
    public async Task ColourHeldByAnotherPlayerInTheGameIsRejectedAsync(string requestedColour)
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(factionColour: requestedColour),
            CancellationToken.None);

        Player unchanged = await LoadOwnSlotAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
            Assert.That(unchanged.FactionColour, Is.Null, "A rejected save leaves the slot exactly as it was.");
        }
    }

    [Test, Description("Colours are unique per game rather than globally, so one already used in a different game is still available here.")]
    public async Task ColourUsedInADifferentGameIsStillAvailableAsync()
    {
        // Free the colour up within this game; the slot in _OTHER_GAME_ID keeps holding it.
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player rival = await context.Players.FindAsync(_RIVAL_PLAYER_ID) ?? throw new InvalidOperationException("Rival slot missing.");
            rival.FactionColour = null;
            await context.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(factionColour: _RIVAL_COLOUR),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Player saved = await LoadOwnSlotAsync();

        Assert.That(saved.FactionColour, Is.EqualTo(_RIVAL_COLOUR));
    }

    [Test, Description("A portrait a rival in the same game already wears is refused, so no two players share a face.")]
    public async Task PortraitHeldByARivalIsRejectedAsync()
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerPortraitPath: _RIVAL_PORTRAIT),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Conflict));
        }
    }

    [Test, Description("The same portrait held in a different game is no obstacle, since the rule is per game rather than global.")]
    public async Task PortraitHeldInAnotherGameIsAcceptedAsync()
    {
        // The slot in _OTHER_GAME_ID wears _RIVAL_PORTRAIT too, so this only passes if the check is scoped by game.
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(gameId: _GAME_ID, rulerPortraitPath: "/portraits/ruler_09.png"),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("A blank portrait lands as the default rather than being refused as a clash with whoever else has none.")]
    public async Task BlankPortraitIsStoredAsTheDefaultAsync()
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerPortraitPath: "   "),
            CancellationToken.None);

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
            Player rival = await context.Players.SingleAsync(p => p.Id == _RIVAL_PLAYER_ID);
            rival.RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH;
            await context.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerPortraitPath: Player.DEFAULT_RULER_PORTRAIT_PATH),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("Re-saving the portrait the caller already wears is not a clash with themselves.")]
    public async Task KeepingTheCallersOwnPortraitIsAcceptedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Player own = await context.Players.SingleAsync(p => p.Id == _OWN_PLAYER_ID);
            own.RulerPortraitPath = "/portraits/ruler_09.png";
            await context.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerPortraitPath: "/portraits/ruler_09.png"),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>
    /// A valid request against the caller's own slot, with only the values a test cares about overridden.
    /// </summary>
    private static UpdatePlayerDefinitionRequest BuildRequest(
        int userId = _USER_ID,
        int gameId = _GAME_ID,
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
            userId,
            gameId,
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

    private async Task<Player> LoadOwnSlotAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Players.FindAsync(_OWN_PLAYER_ID) ?? throw new InvalidOperationException("The caller's slot is missing.");
    }
}
