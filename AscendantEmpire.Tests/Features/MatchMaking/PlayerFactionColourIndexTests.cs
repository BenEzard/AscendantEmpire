using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests that the database, not just the handler, refuses two players in one game the same faction colour.
/// </summary>
/// <remarks>
/// <para>
/// <c>UpdatePlayerDefinitionHandler</c> checks for a clash before it saves, but two players setting themselves up at the same moment can both pass
/// that check. <c>UX_Player_Game_FactionColour</c> is what actually stops them, and the handler only turns the resulting failure into a friendly
/// message - so if the index were ever dropped from the model, every test but this one would still pass.
/// </para>
/// <para>
/// Containerised because it is the constraint itself under test, which is the one thing <c>testing.md</c> says the in-memory provider cannot
/// reproduce. The per-test database is restored from the migrated template, so this also proves the migration created the index.
/// </para>
/// </remarks>
internal class PlayerFactionColourIndexTests : SqlServerTestBase
{
    private const string _COLOUR = "#c800de";

    private int _gameId;
    private int _otherGameId;

    [SetUp]
    public async Task SeedGamesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = BuildGame("Colour Clash Game");
        GameIndex otherGame = BuildGame("Unrelated Game");

        context.GameIndex.AddRange(game, otherGame);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _otherGameId = otherGame.Id;
    }

    [Test, Description("Two slots in one game cannot hold the same faction colour, whatever order the saves arrive in.")]
    public async Task DuplicateColourInOneGameIsRejectedByTheDatabaseAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(BuildPlayer(_gameId, "Faction 1", _COLOUR, campaignOffsetId: 1));
        await context.SaveChangesAsync();

        context.Players.Add(BuildPlayer(_gameId, "Faction 2", _COLOUR, campaignOffsetId: 2));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The index is filtered, so the many slots that have picked no colour yet all sit at NULL without colliding.")]
    public async Task SlotsWithoutAColourDoNotCollideAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(BuildPlayer(_gameId, "Faction 1", factionColour: null, campaignOffsetId: 1));
        context.Players.Add(BuildPlayer(_gameId, "Faction 2", factionColour: null, campaignOffsetId: 2));
        context.Players.Add(BuildPlayer(_gameId, "Faction 3", factionColour: null, campaignOffsetId: 3));

        await context.SaveChangesAsync();

        Assert.That(await context.Players.CountAsync(p => p.GameId == _gameId), Is.EqualTo(3));
    }

    [Test, Description("The index is scoped to the game, so the same colour in a different game is allowed rather than reserved globally.")]
    public async Task SameColourInADifferentGameIsAllowedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(BuildPlayer(_gameId, "Faction 1", _COLOUR, campaignOffsetId: 1));
        context.Players.Add(BuildPlayer(_otherGameId, "Faction 1", _COLOUR, campaignOffsetId: 2));

        await context.SaveChangesAsync();

        Assert.That(await context.Players.CountAsync(p => p.FactionColour == _COLOUR), Is.EqualTo(2));
    }

    /// <summary>
    /// A minimal game row. The gallery id is a plain int with no foreign key behind it, so nothing else needs seeding.
    /// </summary>
    private static GameIndex BuildGame(string gameName)
    {
        return new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = 3,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };
    }

    /// <summary>
    /// An unclaimed slot. <c>UserId</c> stays null so that <c>UX_Player_Game_User</c> never fires and only the colour index is under test.
    /// </summary>
    private static Player BuildPlayer(int gameId, string factionName, string? factionColour, int campaignOffsetId)
    {
        return new Player
        {
            GameId = gameId,
            UserId = null,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = factionName,
            FactionName = factionName,
            FactionColour = factionColour,
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            Status = PlayerStatus.Active,
            CampaignOffsetId = campaignOffsetId,
        };
    }
}
