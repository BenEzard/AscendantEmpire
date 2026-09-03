using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

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
/// Runs at the in-memory tier: the faction colour clash is rejected by the handler's own check, so nothing here depends on the database enforcing the
/// unique index. <see cref="PlayerFactionColourIndexTests"/> covers the index itself.
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
            Status = PlayerStatus.Active,
            CampaignOffsetId = 3,
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("A save writes all five values to the caller's own slot, storing the colour lower-cased so a later clash check cannot miss it.")]
    public async Task SaveWritesEveryValueToTheCallersOwnSlotAsync()
    {
        ServiceResultBase.ServiceResult<ServiceResultBase.Empty> result = await _handler.Handle(
            BuildRequest(rulerTitle: "  Empress  ", rulerName: "  Aelara  ", factionName: "  The Ember Pact  ", factionColour: "#C800DE"),
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
        string factionColour = "#c800de")
    {
        return new UpdatePlayerDefinitionRequest(userId, gameId, rulerTitle, rulerName, rulerGender, factionName, factionColour);
    }

    private async Task<Player> LoadOwnSlotAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.Players.FindAsync(_OWN_PLAYER_ID) ?? throw new InvalidOperationException("The caller's slot is missing.");
    }
}
