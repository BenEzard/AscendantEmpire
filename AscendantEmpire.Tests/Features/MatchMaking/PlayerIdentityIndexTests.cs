using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests that the database, not just the handler, refuses two players in one game the same ruler name, faction name, people name, demonym, culture
/// name or chosen portrait - and that it compares them case-insensitively.
/// </summary>
/// <remarks>
/// <para>
/// <c>UpdatePlayerDefinitionHandler</c> checks for each of these before it saves, but two players setting themselves up at the same moment can both
/// pass that check. These indexes are what actually stop them, and the handler only turns the resulting failure into a friendly message - so if one
/// were dropped from the model, every test but these would still pass.
/// </para>
/// <para>
/// The case-insensitivity is worth a test of its own because it rests on <see cref="Player.IDENTITY_COLLATION"/> being pinned on the columns. Nothing
/// else in the solution sets a collation, so without that these indexes would mean whatever the server that created the database was configured for.
/// </para>
/// <para>
/// Containerised because it is the constraints themselves under test, which is the one thing <c>testing.md</c> says the in-memory provider cannot
/// reproduce. The per-test database is restored from the migrated template, so this also proves the migration created the indexes. The sibling
/// <see cref="PlayerFactionColourIndexTests"/> covers the faction colour's.
/// </para>
/// </remarks>
internal class PlayerIdentityIndexTests : SqlServerTestBase
{
    private const string _PORTRAIT = "/portraits/ruler_04.png";

    private int _gameId;
    private int _otherGameId;

    [SetUp]
    public async Task SeedGamesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = BuildGame("Identity Clash Game");
        GameIndex otherGame = BuildGame("Unrelated Game");

        context.GameIndex.AddRange(game, otherGame);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _otherGameId = otherGame.Id;
    }

    [TestCase("RulerName", Description = "Two slots in one game cannot hold the same ruler name, whatever order the saves arrive in.")]
    [TestCase("FactionName", Description = "Two slots in one game cannot hold the same faction name, whatever order the saves arrive in.")]
    [TestCase("PeopleName", Description = "Two slots in one game cannot hold the same people name, which used to be shareable flavour text.")]
    [TestCase("Demonym", Description = "Two slots in one game cannot hold the same demonym, whatever order the saves arrive in.")]
    [TestCase("CultureName", Description = "Two slots in one game cannot hold the same culture name, whatever order the saves arrive in.")]
    public async Task DuplicateNameInOneGameIsRejectedByTheDatabaseAsync(string field)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(BuildPlayer(_gameId, suffix: 1));
        await context.SaveChangesAsync();

        Player second = BuildPlayer(_gameId, suffix: 2);
        SetField(second, field, ReadField(BuildPlayer(_gameId, suffix: 1), field));
        context.Players.Add(second);

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [TestCase("RulerName", Description = "The ruler name index is case-insensitive, so the same name in different capitals is one name rather than two.")]
    [TestCase("FactionName", Description = "The faction name index is case-insensitive, so the same name in different capitals is one name rather than two.")]
    [TestCase("PeopleName", Description = "The people name index is case-insensitive, so \"The Vashti\" and \"the vashti\" cannot both be taken.")]
    [TestCase("Demonym", Description = "The demonym index is case-insensitive, so the same demonym in different capitals is one rather than two.")]
    [TestCase("CultureName", Description = "The culture name index is case-insensitive, so the same name in different capitals is one rather than two.")]
    public async Task NameDifferingOnlyInCaseIsRejectedByTheDatabaseAsync(string field)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player first = BuildPlayer(_gameId, suffix: 1);
        SetField(first, field, "The Vashti");
        context.Players.Add(first);
        await context.SaveChangesAsync();

        Player second = BuildPlayer(_gameId, suffix: 2);
        SetField(second, field, "the vashti");
        context.Players.Add(second);

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The indexes are scoped to the game, so one player's whole identity is available again in a different game rather than reserved globally.")]
    public async Task SameIdentityInADifferentGameIsAllowedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player here = BuildPlayer(_gameId, suffix: 1);
        Player elsewhere = BuildPlayer(_otherGameId, suffix: 1);
        elsewhere.CampaignOffsetId = 2;

        context.Players.AddRange(here, elsewhere);

        await context.SaveChangesAsync();

        Assert.That(await context.Players.CountAsync(p => p.PeopleName == "People 1"), Is.EqualTo(2));
    }

    [Test, Description("A language is deliberately outside the rule, so two peoples in one game may speak the same one.")]
    public async Task SameLanguageInOneGameIsAllowedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player first = BuildPlayer(_gameId, suffix: 1);
        Player second = BuildPlayer(_gameId, suffix: 2);
        first.Language = "Old Vashti";
        second.Language = "Old Vashti";

        context.Players.AddRange(first, second);

        await context.SaveChangesAsync();

        Assert.That(await context.Players.CountAsync(p => p.Language == "Old Vashti"), Is.EqualTo(2));
    }

    [Test, Description("Two slots in one game cannot wear the same chosen portrait, so no two rulers share a face even if both saves pass the handler's check.")]
    public async Task DuplicatePortraitInOneGameIsRejectedByTheDatabaseAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player first = BuildPlayer(_gameId, suffix: 1);
        first.RulerPortraitPath = _PORTRAIT;
        context.Players.Add(first);
        await context.SaveChangesAsync();

        Player second = BuildPlayer(_gameId, suffix: 2);
        second.RulerPortraitPath = _PORTRAIT;
        context.Players.Add(second);

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The portrait index is filtered, so the many slots that have chosen none all sit on the default without colliding.")]
    public async Task SlotsWithoutAPortraitDoNotCollideAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Players.Add(BuildPlayer(_gameId, suffix: 1));
        context.Players.Add(BuildPlayer(_gameId, suffix: 2));
        context.Players.Add(BuildPlayer(_gameId, suffix: 3));

        await context.SaveChangesAsync();

        Assert.That(await context.Players.CountAsync(p => p.GameId == _gameId), Is.EqualTo(3));
    }

    /// <summary>Reads one of the five unique names off a slot by name, so a single test case can stand for all five.</summary>
    private static string ReadField(Player player, string field)
    {
        return field switch
        {
            "RulerName" => player.RulerName,
            "FactionName" => player.FactionName,
            "PeopleName" => player.PeopleName,
            "Demonym" => player.Demonym,
            "CultureName" => player.CultureName,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not one of the five unique identity names.")
        };
    }

    /// <summary>Writes one of the five unique names on a slot by name. The counterpart to <see cref="ReadField"/>.</summary>
    private static void SetField(Player player, string field, string value)
    {
        switch (field)
        {
            case "RulerName":
                player.RulerName = value;
                break;
            case "FactionName":
                player.FactionName = value;
                break;
            case "PeopleName":
                player.PeopleName = value;
                break;
            case "Demonym":
                player.Demonym = value;
                break;
            case "CultureName":
                player.CultureName = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Not one of the five unique identity names.");
        }
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
    /// An unclaimed slot carrying the placeholders game setup seeds, suffixed per slot so nothing collides by accident.
    /// </summary>
    /// <remarks>
    /// <c>UserId</c> stays null so <c>UX_Player_Game_User</c> never fires, the colour stays null so
    /// <c>UX_Player_Game_FactionColour</c> never does either, and the portrait stays on the default - leaving only the index a given test is about.
    /// </remarks>
    private static Player BuildPlayer(int gameId, int suffix)
    {
        return new Player
        {
            GameId = gameId,
            UserId = null,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = $"Player {suffix}",
            FactionName = $"Faction {suffix}",
            FactionColour = null,
            PeopleName = $"People {suffix}",
            Demonym = $"Person {suffix}",
            CultureName = $"Culture {suffix}",
            Language = $"Language {suffix}",
            RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
            Status = PlayerStatus.Active,
            CampaignOffsetId = suffix,
        };
    }
}
