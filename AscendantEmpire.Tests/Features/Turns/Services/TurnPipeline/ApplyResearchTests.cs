using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="ApplyResearch"/> — where the Turn's directed points go when it closes, and what is left behind in the books.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process loads and saves through the change tracker, and the real
/// <see cref="PlayerTechnologyAvailabilityService"/> it collaborates with is a graph walk over ordinary LINQ reads — neither uses
/// <c>ExecuteUpdateAsync</c>, raw SQL or a transaction. The real service is used rather than a substitute precisely so the discovery cascade can be
/// asserted end to end, which is the half of discovery a Player actually sees.
/// </para>
/// <para>
/// The bucket arithmetic is pinned separately, without a database, in <see cref="ResearchBucketFillTests"/>. What is tested here is the orchestration
/// around it: the once-per-Round guard, the discovery stamps, and the history row.
/// </para>
/// </remarks>
[TestFixture]
internal class ApplyResearchTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _PLAYER_TURN_ID = 42;
    private const int _CURRENT_ROUND_NUMBER = 3;

    /// <summary>The Technology every test researches unless it says otherwise.</summary>
    private const int _PROJECT_ID = 1;

    /// <summary>A Technology gated behind <see cref="_PROJECT_ID"/>, so the discovery cascade has somewhere to land.</summary>
    private const int _DEPENDENT_ID = 2;

    private ApplyResearch _process = null!;

    [SetUp]
    public async Task SetUp()
    {
        _process = new ApplyResearch(
            DbFactory,
            new PlayerTechnologyAvailabilityService(NullLogger<PlayerTechnologyAvailabilityService>.Instance),
            NullLogger<ApplyResearch>.Instance);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Technologies.Add(CatalogueTechnology(_PROJECT_ID, "Project"));
        context.Technologies.Add(CatalogueTechnology(_DEPENDENT_ID, "Dependent"));

        context.TechnologyPrerequisites.Add(new TechnologyPrerequisite
        {
            TechnologyId = _DEPENDENT_ID,
            PrerequisiteTechnologyId = _PROJECT_ID,
            PrerequisiteKind = TechnologyPrerequisiteKind.Mandatory
        });

        context.PlayerTechnologies.Add(PlayerRow(_DEPENDENT_ID, TechnologyStatus.Locked, directedRequired: 500));

        await context.SaveChangesAsync();
    }

    [Test, Description("The Turn's whole pool goes into the project when the project can take it, which is the ordinary case the whole feature exists for.")]
    public async Task ShouldSpendTheWholePoolOnTheProjectAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: _PROJECT_ID);

        string error = await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        PlayerResearchState state = await ReloadStateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(project.DirectedPoints, Is.EqualTo(120));
            Assert.That(state.UnspentDirectedPoints, Is.Zero, "The pool always closes the Turn at zero, so this Turn's figure cannot leak into the next.");
        }
    }

    [Test, Description("With no project selected the pool is still emptied, because the task's rule is that the Turn ends at zero whether or not there was anywhere to put the points.")]
    public async Task ShouldEmptyThePoolWhenNoProjectIsSelectedAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        PlayerResearchState state = await ReloadStateAsync();
        ResearchHistory history = await ReloadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.UnspentDirectedPoints, Is.Zero);
            Assert.That(project.DirectedPoints, Is.Zero, "Nothing was selected, so nothing was researched by accident.");
            Assert.That(history.Lost, Is.EqualTo(120), "Recorded as lost rather than silently disappearing, so the Player can see what the choice cost them.");
        }
    }

    [Test, Description("A project whose directed bucket is short takes everything into that bucket first, leaving the passive buckets empty — directed points stand in for them only once their own bucket is complete.")]
    public async Task ShouldFillTheDirectedBucketBeforeTheOthersAsync()
    {
        await AddProjectAsync(directedRequired: 500, experienceRequired: 250, diffusionRequired: 250);
        await AddResearchStateAsync(unspentDirectedPoints: 400, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.DirectedPoints, Is.EqualTo(400));
            Assert.That(project.ExperiencePoints, Is.Zero);
            Assert.That(project.DiffusionPoints, Is.Zero);
        }
    }

    [Test, Description("A pool that outruns the directed bucket spills into the experience and diffusion buckets of the same Technology, because directed research can complete a technology without either of them accruing.")]
    public async Task ShouldSpillIntoThePassiveBucketsOfTheSameTechnologyAsync()
    {
        await AddProjectAsync(directedRequired: 100, experienceRequired: 50, diffusionRequired: 50);
        await AddResearchStateAsync(unspentDirectedPoints: 175, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.DirectedPoints, Is.EqualTo(100));
            Assert.That(project.ExperiencePoints, Is.EqualTo(50));
            Assert.That(project.DiffusionPoints, Is.EqualTo(25));
        }
    }

    [Test, Description("Points beyond everything the project needs are lost rather than carried onto another Technology, because overflow is a separate rule that is not switched on.")]
    public async Task ShouldLoseWhatTheProjectCannotTakeAsync()
    {
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 250, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        PlayerTechnology dependent = await ReloadDependentAsync();
        ResearchHistory history = await ReloadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.DirectedPoints, Is.EqualTo(100));
            Assert.That(dependent.DirectedPoints, Is.Zero, "Nothing overflowed onto the Technology that just became available.");
            Assert.That(history.Spent, Is.EqualTo(100));
            Assert.That(history.Lost, Is.EqualTo(150));
            Assert.That(history.Overflowed, Is.Zero);
        }
    }

    [Test, Description("Filling the last bucket discovers the Technology and stamps how and when, so the discovery is a fact in the data rather than something a reader has to infer from the point totals.")]
    public async Task ShouldDiscoverTheTechnologyWhenTheLastBucketFillsAsync()
    {
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.Status, Is.EqualTo(TechnologyStatus.Discovered));
            Assert.That(project.DiscoveredOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
            Assert.That(project.DiscoveryMethod, Is.EqualTo(TechnologyDiscoveryMethod.DirectedResearch));
            Assert.That(project.AcquisitionPaidOnRound, Is.EqualTo(1), "The cost was settled when the project was chosen and stays settled.");
        }
    }

    [Test, Description("A three-bucket Technology can be discovered from directed research alone, with neither experience nor diffusion ever having accrued — the design has directed research able to complete a technology by itself.")]
    public async Task ShouldDiscoverAThreeBucketTechnologyFromDirectedResearchAloneAsync()
    {
        await AddProjectAsync(directedRequired: 100, experienceRequired: 50, diffusionRequired: 50);
        await AddResearchStateAsync(unspentDirectedPoints: 200, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        Assert.That(project.Status, Is.EqualTo(TechnologyStatus.Discovered));
    }

    [Test, Description("Discovery clears the selection, so the Player is prompted to choose again. Left in place they would select a Discovered Technology, spend nothing every Round, and never be nudged — the end-turn check fires only on a null selection.")]
    public async Task ShouldClearTheSelectionOnDiscoveryAsync()
    {
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.CurrentTechnologyId, Is.Null);
    }

    [Test, Description("Discovery makes the dependents of what was discovered available, which is the cascade the whole tree advances by and the only visible reward for finishing a project.")]
    public async Task ShouldMakeTheDependentAvailableOnDiscoveryAsync()
    {
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology dependent = await ReloadDependentAsync();

        Assert.That(dependent.Status, Is.EqualTo(TechnologyStatus.Available));
    }

    [Test, Description("A Technology is not discovered while any bucket it uses is short, so a part-filled project stays Unlocked and keeps absorbing.")]
    public async Task ShouldNotDiscoverWhileABucketIsShortAsync()
    {
        await AddProjectAsync(directedRequired: 100, experienceRequired: 50);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        PlayerTechnology dependent = await ReloadDependentAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.Status, Is.EqualTo(TechnologyStatus.Unlocked));
            Assert.That(project.DiscoveredOnRound, Is.Null);
            Assert.That(dependent.Status, Is.EqualTo(TechnologyStatus.Locked), "Nothing cascaded.");
        }
    }

    [Test, Description("Exactly one directed history row is written per Player per Round, carrying what was produced, what went in and what went nowhere — it is what answers \"why did I only get that many points?\".")]
    public async Task ShouldWriteOneDirectedHistoryRowAsync()
    {
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 130, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        List<ResearchHistory> rows = await context.ResearchHistories.ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Bucket, Is.EqualTo(ResearchBucket.Directed));
            Assert.That(rows[0].GameId, Is.EqualTo(_GAME_ID));
            Assert.That(rows[0].PlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(rows[0].PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(rows[0].RoundNumber, Is.EqualTo(_CURRENT_ROUND_NUMBER));
            Assert.That(rows[0].Generated, Is.EqualTo(130));
            Assert.That(rows[0].Spent, Is.EqualTo(100));
            Assert.That(rows[0].Lost, Is.EqualTo(30));
        }
    }

    [Test, Description("The Round stamp is advanced to the Round the spend happened in, so a spend this Round can be told from one made earlier.")]
    public async Task ShouldStampTheRoundTheSpendHappenedInAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerResearchState state = await ReloadStateAsync();

        Assert.That(state.LastResearchAppliedOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
    }

    [Test, Description("Running twice in one Round spends once. A retried end of Turn would otherwise pour a second pool into the project and write a second history row for the same Round.")]
    public async Task ShouldApplyOnlyOncePerRoundAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        // Stands in for a start-of-Turn generation that has already run again, which is the only way the pool could be non-empty on a second pass.
        await SetUnspentDirectedPointsAsync(100);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        int historyCount = await context.ResearchHistories.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.DirectedPoints, Is.EqualTo(100), "The second run spent nothing.");
            Assert.That(historyCount, Is.EqualTo(1));
        }
    }

    [Test, Description("A project that is not Unlocked absorbs nothing. Directed research needs the acquisition cost settled, and a Technology discovered by some other method while still selected must not keep taking points.")]
    public async Task ShouldSpendNothingOnAProjectThatIsNotUnlockedAsync()
    {
        await AddProjectAsync(directedRequired: 500, status: TechnologyStatus.Available, acquisitionPaidOnRound: null);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: _PROJECT_ID);

        string error = await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        ResearchHistory history = await ReloadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty, "The pipeline is fail-fast, so a bad selection must not block the Player's Turn.");
            Assert.That(project.DirectedPoints, Is.Zero);
            Assert.That(history.Lost, Is.EqualTo(120));
        }
    }

    [Test, Description("A Player with no research state row is skipped rather than failing the Turn, because a Game set up before the Technology schema existed must still be playable.")]
    public async Task ShouldSkipAPlayerWithNoResearchStateAsync()
    {
        await AddProjectAsync(directedRequired: 500);

        string error = await _process.ProcessAsync(EndTurnContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            Assert.That(await context.ResearchHistories.AnyAsync(), Is.False, "Nothing happened, so nothing was recorded.");
        }
    }

    [Test, Description("An end-of-Turn context carrying no Player Id fails the pipeline rather than spending every Player's points.")]
    public async Task ShouldFailWhenTheContextCarriesNoPlayerAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: _PROJECT_ID);

        var contextWithNoPlayer = new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(contextWithNoPlayer);

        PlayerResearchState state = await ReloadStateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Not.Empty);
            Assert.That(state.UnspentDirectedPoints, Is.EqualTo(120), "Nothing was spent and nothing was emptied.");
        }
    }

    [Test, Description("An end-of-Turn context carrying no Player Turn Id fails, because the Round's research summary is recorded against the Turn and there would be nothing to stamp it with.")]
    public async Task ShouldFailWhenTheContextCarriesNoPlayerTurnAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 120, currentTechnologyId: _PROJECT_ID);

        var contextWithNoTurn = new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, _CURRENT_ROUND_NUMBER);

        string error = await _process.ProcessAsync(contextWithNoTurn);

        Assert.That(error, Is.Not.Empty);
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static TurnPipelineContextData EndTurnContext() =>
        new(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, _CURRENT_ROUND_NUMBER)
        {
            PlayerTurnId = _PLAYER_TURN_ID
        };

    private static Technology CatalogueTechnology(int id, string code) =>
        new()
        {
            Id = id,
            Code = code,
            DisplayName = code,
            IconPath = $"/technology/{code.ToLowerInvariant()}.png",
            Era = TechnologyEra.Stone,
            SortOrder = id,
            DirectedRequired = 500
        };

    private static PlayerTechnology PlayerRow(
        int technologyId,
        TechnologyStatus status,
        int? directedRequired = null,
        int? experienceRequired = null,
        int? diffusionRequired = null,
        int? acquisitionPaidOnRound = null) =>
        new()
        {
            PlayerId = _PLAYER_ID,
            TechnologyId = technologyId,
            GameId = _GAME_ID,
            Status = status,
            DirectedRequired = directedRequired,
            ExperienceRequired = experienceRequired,
            DiffusionRequired = diffusionRequired,
            AcquisitionPaidOnRound = acquisitionPaidOnRound
        };

    /// <summary>Adds the Player's row for the project, defaulting to an Unlocked Technology whose cost was settled in Round 1.</summary>
    private async Task AddProjectAsync(
        int? directedRequired,
        int? experienceRequired = null,
        int? diffusionRequired = null,
        TechnologyStatus status = TechnologyStatus.Unlocked,
        int? acquisitionPaidOnRound = 1)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerTechnologies.Add(PlayerRow(
            _PROJECT_ID,
            status,
            directedRequired,
            experienceRequired,
            diffusionRequired,
            acquisitionPaidOnRound));

        await context.SaveChangesAsync();
    }

    private async Task AddResearchStateAsync(int unspentDirectedPoints, int? currentTechnologyId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerResearchStates.Add(new PlayerResearchState
        {
            PlayerId = _PLAYER_ID,
            GameId = _GAME_ID,
            TechPointsPerPopulationSize = 100,
            CurrentTechnologyId = currentTechnologyId,
            UnspentDirectedPoints = unspentDirectedPoints
        });

        await context.SaveChangesAsync();
    }

    private async Task SetUnspentDirectedPointsAsync(int unspentDirectedPoints)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerResearchState state = await context.PlayerResearchStates.SingleAsync(row => row.PlayerId == _PLAYER_ID);
        state.UnspentDirectedPoints = unspentDirectedPoints;

        await context.SaveChangesAsync();
    }

    private async Task<PlayerTechnology> ReloadProjectAsync() => await ReloadPlayerTechnologyAsync(_PROJECT_ID);

    private async Task<PlayerTechnology> ReloadDependentAsync() => await ReloadPlayerTechnologyAsync(_DEPENDENT_ID);

    private async Task<PlayerTechnology> ReloadPlayerTechnologyAsync(int technologyId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerTechnologies.SingleAsync(row => row.PlayerId == _PLAYER_ID && row.TechnologyId == technologyId);
    }

    private async Task<PlayerResearchState> ReloadStateAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerResearchStates.SingleAsync(row => row.PlayerId == _PLAYER_ID);
    }

    private async Task<ResearchHistory> ReloadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.ResearchHistories.SingleAsync(row => row.PlayerId == _PLAYER_ID);
    }
}
