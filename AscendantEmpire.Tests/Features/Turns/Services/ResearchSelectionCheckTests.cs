using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;

using AscendantEmpire.Application.Data;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Tests the end-turn nudge for a Player who has research available and no project chosen.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the check is two LINQ reads. Both halves of its condition are worth pinning, and the second is the one that is easy to get wrong - a Player
/// whose every technology is still Locked must not be nagged every Round about a choice they cannot make.
/// </para>
/// <para>
/// A Discovered technology is not researchable either, so it must not count as something to research - otherwise a Player who had finished everything
/// reachable would be told to pick a project with nothing to pick.
/// </para>
/// </remarks>
[TestFixture]
internal class ResearchSelectionCheckTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _CURRENT_ROUND = 3;

    private AscendantEmpireContext _context = null!;
    private ResearchSelectionCheck _check = null!;
    private int _playerId;
    private int _technologyId;

    [SetUp]
    public async Task SetUpCheck()
    {
        _context = await DbFactory.CreateDbContextAsync();
        _check = new ResearchSelectionCheck(DbFactory);

        _context.GameIndex.Add(new GameIndex
        {
            Id = _GAME_ID,
            GameName = "Fixture",
            CampaignMapIndexId = 1,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.AwaitingPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30
        });

        var player = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = "Chief",
            RulerName = "Fixture",
            FactionName = "Fixture",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png"
        };

        var technology = new Technology
        {
            Code = "Hunting",
            DisplayName = "Hunting",
            IconPath = string.Empty,
            Era = TechnologyEra.Stone,
            SortOrder = 10,
            DirectedRequired = 100
        };

        _context.Players.Add(player);
        _context.Technologies.Add(technology);
        await _context.SaveChangesAsync();

        _playerId = player.Id;
        _technologyId = technology.Id;

        _context.PlayerResearchStates.Add(new PlayerResearchState
        {
            PlayerId = _playerId,
            GameId = _GAME_ID
        });

        await _context.SaveChangesAsync();
    }

    [TearDown]
    public void DisposeContext()
    {
        _context?.Dispose();
    }

    [Test, Description("A Player with something researchable and no project chosen is told so, which is the whole point of the check.")]
    public async Task ShouldReportTheGapWhenNothingIsSelectedAndSomethingIsResearchable()
    {
        await SeedPlayerTechnologyAsync(TechnologyStatus.Available);

        PossibleAction action = (await EvaluateAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(action.Category, Is.EqualTo(InformationCategory.Technology));
            Assert.That(action.Description, Is.EqualTo("No research project is selected."));

            // Research is empire-wide, so there is no record behind it and no tile to pan to.
            Assert.That(action.RecordTypeId, Is.EqualTo((int)RecordType.None));
            Assert.That(action.Coord, Is.Null);
        });
    }

    [Test, Description("An Unlocked technology counts as something to research, because directed research can only go into one that is at least unlocked.")]
    public async Task ShouldReportTheGapWhenTheOnlyOptionIsUnlocked()
    {
        await SeedPlayerTechnologyAsync(TechnologyStatus.Unlocked, acquisitionPaidOnRound: 1);

        Assert.That(await EvaluateAsync(), Has.Count.EqualTo(1));
    }

    [Test, Description("A Player who has already chosen a project is not asked again, whatever else is available to them.")]
    public async Task ShouldReportNothingWhenAProjectIsAlreadySelected()
    {
        await SeedPlayerTechnologyAsync(TechnologyStatus.Available);
        await SelectProjectAsync();

        Assert.That(await EvaluateAsync(), Is.Empty);
    }

    [Test, Description("A Player whose every technology is Locked is not nagged, because they cannot act on the prompt - the opening state of every game.")]
    public async Task ShouldReportNothingWhenEverythingIsStillLocked()
    {
        await SeedPlayerTechnologyAsync(TechnologyStatus.Locked);

        Assert.That(await EvaluateAsync(), Is.Empty);
    }

    [Test, Description("A Discovered technology is not something left to research, so it must not stand in for an option the Player could choose.")]
    public async Task ShouldReportNothingWhenTheOnlyTechnologyIsAlreadyDiscovered()
    {
        await SeedPlayerTechnologyAsync(TechnologyStatus.Discovered, discoveredOnRound: 1);

        Assert.That(await EvaluateAsync(), Is.Empty);
    }

    [Test, Description("A Player with no rows at all is not nagged, which is how a Player set up before the technology schema existed presents.")]
    public async Task ShouldReportNothingWhenThePlayerHasNoTechnologyRows()
    {
        Assert.That(await EvaluateAsync(), Is.Empty);
    }

    // --- Helpers ----------------------------------------------------------------------

    private async Task<IReadOnlyList<PossibleAction>> EvaluateAsync()
    {
        return await _check.EvaluateAsync(new PossibleActionContext(_GAME_ID, _playerId, _CURRENT_ROUND), CancellationToken.None);
    }

    private async Task SeedPlayerTechnologyAsync(
        TechnologyStatus status,
        int? acquisitionPaidOnRound = null,
        int? discoveredOnRound = null)
    {
        _context.PlayerTechnologies.Add(new PlayerTechnology
        {
            PlayerId = _playerId,
            TechnologyId = _technologyId,
            GameId = _GAME_ID,
            Status = status,
            DirectedRequired = 100,
            AcquisitionPaidOnRound = acquisitionPaidOnRound,
            DiscoveredOnRound = discoveredOnRound
        });

        await _context.SaveChangesAsync();
    }

    private async Task SelectProjectAsync()
    {
        PlayerResearchState state = await _context.PlayerResearchStates.FindAsync(_playerId)
            ?? throw new InvalidOperationException("The fixture's research state is missing.");

        state.CurrentTechnologyId = _technologyId;

        await _context.SaveChangesAsync();
    }
}
