using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for reading back the caller's own player definition, and the rival colours that travel with it.
/// </summary>
/// <remarks>
/// <para>
/// The used-colour list is what the setup dialog disables swatches from, so the two things worth pinning are that it names every rival holding a
/// colour and that it never names the caller. Including the caller would strike through the colour they are already using and make it look taken.
/// </para>
/// <para>
/// Runs at the in-memory tier: one LINQ query with the splitting done client-side after materialising, so nothing here needs a real database.
/// </para>
/// </remarks>
internal class GetPlayerDefinitionHandlerTests : InMemoryTestBase
{
    private const int _USER_ID = 11;
    private const int _GAME_ID = 4;
    private const int _GAME_WITHOUT_THE_CALLER = 5;

    private const int _OWN_PLAYER_ID = 1;

    private const string _OWN_COLOUR = "#c800de";
    private const string _RIVAL_COLOUR = "#c10007";

    private const string _OWN_PORTRAIT = "/portraits/ruler_07.png";
    private const string _RIVAL_PORTRAIT = "/portraits/ruler_04.png";

    private GetPlayerDefinitionHandler _handler = null!;

    [SetUp]
    public async Task SeedPlayersAsync()
    {
        _handler = new GetPlayerDefinitionHandler(NullLogger<GetPlayerDefinitionHandler>.Instance, DbFactory);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // The games themselves, because the definition now reports whether it is locked and that is read off the play status.
        context.GameIndex.Add(BuildGame(_GAME_ID, "Definition Game", GamePlayStatus.AwaitingPlayers));
        context.GameIndex.Add(BuildGame(_GAME_WITHOUT_THE_CALLER, "Someone Else's Game", GamePlayStatus.AwaitingPlayers));

        // The caller's own slot: defined apart from its gender, which stands in for a slot whose
        // owner has not opened the setup dialog yet.
        context.Players.Add(new Player
        {
            Id = _OWN_PLAYER_ID,
            GameId = _GAME_ID,
            UserId = _USER_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Player 1",
            FactionName = "Faction 1",
            FactionColour = _OWN_COLOUR,
            // Distinct from the other slots' placeholders, so the read-back assertions below would catch the
            // handler reading these four off the wrong row.
            PeopleName = "The Vashti",
            Demonym = "Vashti",
            CultureName = "Vashti Highland",
            Language = "Old Vashti",
            RulerPortraitPath = _OWN_PORTRAIT,
            RulerGender = null,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        });

        // A rival holding a colour, and one who has picked none yet.
        context.Players.Add(new Player
        {
            Id = 2,
            GameId = _GAME_ID,
            UserId = 12,
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

        context.Players.Add(new Player
        {
            Id = 3,
            GameId = _GAME_ID,
            UserId = null,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Player 3",
            FactionName = "Faction 3",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            // Still on the default, like every slot a game seeds: holds no portrait, so takes none out of the set.
            RulerPortraitPath = Player.DEFAULT_RULER_PORTRAIT_PATH,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 3,
        });

        // A game the caller holds no slot in.
        context.Players.Add(new Player
        {
            Id = 4,
            GameId = _GAME_WITHOUT_THE_CALLER,
            UserId = 12,
            RulerTitle = "King",
            RulerName = "Elsewhere",
            FactionName = "Other Faction",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            // The caller's own portrait, in a game they hold no slot in: it must not appear as taken.
            RulerPortraitPath = _OWN_PORTRAIT,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 4,
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("The definition describes the caller's own slot, reporting a null gender rather than a default for one nobody has defined.")]
    public async Task DefinitionDescribesTheCallersOwnSlotAsync()
    {
        ServiceResultBase.ServiceResult<PlayerDefinition> result =
            await _handler.Handle(new GetPlayerDefinitionRequest(_USER_ID, _GAME_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        PlayerDefinition definition = result.SuccessValue!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(definition.PlayerId, Is.EqualTo(_OWN_PLAYER_ID));
            Assert.That(definition.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(definition.RulerTitle, Is.EqualTo(Player.DEFAULT_RULER_TITLE));
            Assert.That(definition.RulerName, Is.EqualTo("Player 1"));
            Assert.That(definition.RulerGender, Is.Null, "An undefined slot asserts no gender at all.");
            Assert.That(definition.FactionName, Is.EqualTo("Faction 1"));
            Assert.That(definition.FactionColour, Is.EqualTo(_OWN_COLOUR));
            Assert.That(definition.PeopleName, Is.EqualTo("The Vashti"));
            Assert.That(definition.Demonym, Is.EqualTo("Vashti"));
            Assert.That(definition.CultureName, Is.EqualTo("Vashti Highland"));
            Assert.That(definition.Language, Is.EqualTo("Old Vashti"));
            Assert.That(definition.RulerPortraitPath, Is.EqualTo(_OWN_PORTRAIT), "The caller's own portrait, which the rivals' differs from.");
        }
    }

    [Test, Description("The used-portrait list names only rivals who have chosen one, never the caller and never a rival still on the default.")]
    public async Task UsedPortraitsCoverChosenRivalsOnlyAsync()
    {
        ServiceResultBase.ServiceResult<PlayerDefinition> result =
            await _handler.Handle(new GetPlayerDefinitionRequest(_USER_ID, _GAME_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        IReadOnlyCollection<UsedRulerPortrait> usedPortraits = result.SuccessValue!.UsedRulerPortraits;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(usedPortraits.Select(p => p.Value), Is.EqualTo(new[] { _RIVAL_PORTRAIT }),
                "Only the rival who has chosen a portrait: the undecided slot holds the default, which is the one value a game may repeat, and the caller's own must stay pickable.");
            Assert.That(usedPortraits.Single().UsedBy, Is.EqualTo("Rival Faction"), "Named by faction, so the picker can say who holds it.");
        }
    }

    [Test, Description("The used-colour list names every rival holding a colour and never the caller, whose own colour must stay pickable.")]
    public async Task UsedColoursCoverTheRivalsButNotTheCallerAsync()
    {
        ServiceResultBase.ServiceResult<PlayerDefinition> result =
            await _handler.Handle(new GetPlayerDefinitionRequest(_USER_ID, _GAME_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        IReadOnlyCollection<UsedFactionColour> usedColours = result.SuccessValue!.UsedFactionColours;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(usedColours, Has.Count.EqualTo(1), "Only the one rival that has picked a colour is listed.");
            Assert.That(usedColours.Single().Value, Is.EqualTo(_RIVAL_COLOUR));
            Assert.That(usedColours.Single().UsedBy, Is.EqualTo("Rival Faction"), "The faction is named so the picker can say who holds the colour.");
            Assert.That(usedColours.Select(c => c.Value), Does.Not.Contain(_OWN_COLOUR));
        }
    }

    [Test, Description("Asking about a game the caller holds no slot in is a 404, not an empty definition that the dialog would try to save.")]
    public async Task GameTheCallerHoldsNoSlotInIsNotFoundAsync()
    {
        ServiceResultBase.ServiceResult<PlayerDefinition> result =
            await _handler.Handle(new GetPlayerDefinitionRequest(_USER_ID, _GAME_WITHOUT_THE_CALLER), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [TestCase(GamePlayStatus.AwaitingPlayers, false, Description = "A game still filling up reports an unlocked definition, so every field stays editable in the dialog.")]
    [TestCase(GamePlayStatus.InProgress, true, Description = "A game under way reports a locked definition, so the dialog disables the fields a save would now ignore.")]
    [TestCase(GamePlayStatus.Completed, true, Description = "A finished game stays locked, matching the handler that writes definitions.")]
    [TestCase(GamePlayStatus.Abandoned, true, Description = "An abandoned game stays locked, matching the handler that writes definitions.")]
    public async Task LockMirrorsThePlayStatusAsync(GamePlayStatus playStatus, bool expectedLock)
    {
        await using (AscendantEmpireContext arrangeContext = await DbFactory.CreateDbContextAsync())
        {
            GameIndex game = await arrangeContext.GameIndex.FindAsync(_GAME_ID) ?? throw new InvalidOperationException("The game is missing.");
            game.PlayStatus = playStatus;
            await arrangeContext.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<PlayerDefinition> result =
            await _handler.Handle(new GetPlayerDefinitionRequest(_USER_ID, _GAME_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        Assert.That(result.SuccessValue!.IsLocked, Is.EqualTo(expectedLock));
    }

    /// <summary>A minimal game row with an explicit Id, so the seeded slots can point at it.</summary>
    private static GameIndex BuildGame(int gameId, string gameName, GamePlayStatus playStatus)
    {
        return new GameIndex
        {
            Id = gameId,
            CampaignMapIndexId = 1,
            GameName = gameName,
            NumberOfPlayers = 3,
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
}
