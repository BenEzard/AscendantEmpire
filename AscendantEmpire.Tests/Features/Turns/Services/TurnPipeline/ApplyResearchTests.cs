using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
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

    /// <summary>The one kind of experience the drain tests accrue, linked to whichever Technologies a test says consume it.</summary>
    private const int _EXPERIENCE_TYPE_ID = 11;

    /// <summary>The code the derived movement rule earns against, so a tile step can be followed all the way into a bucket.</summary>
    private const string _JUNGLE_CODE = "Biome.Jungle";

    private ApplyResearch _process = null!;

    [SetUp]
    public async Task SetUp()
    {
        _process = new ApplyResearch(
            DbFactory,
            new PlayerTechnologyAvailabilityService(NullLogger<PlayerTechnologyAvailabilityService>.Instance),
            new PlayerTechnologySustenanceModifierService(NullLogger<PlayerTechnologySustenanceModifierService>.Instance),
            new PlayerTechnologyExtractionEfficiencyService(NullLogger<PlayerTechnologyExtractionEfficiencyService>.Instance),
            new ExperienceAllocator(NullLogger<ExperienceAllocator>.Instance),
            new ExperienceAccrualReader(NullLogger<ExperienceAccrualReader>.Instance),
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

    #region Directed spend

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

    [Test, Description("Discovering a Technology grants its sustenance modifiers in the same Turn, not the one after - the reconcile runs before the discovery is saved.")]
    public async Task ShouldGrantSustenanceModifiersOnDiscoveryAsync()
    {
        await AddSustenanceModifierGrantAsync();
        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        List<PlayerSustenanceModifier> modifiers = await ReloadModifiersAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers, Has.Count.EqualTo(1));
            Assert.That(modifiers[0].SourceType, Is.EqualTo(ModifierSourceType.Technology));
            Assert.That(modifiers[0].SourceId, Is.EqualTo(_PROJECT_ID));
            Assert.That(modifiers[0].Resource, Is.EqualTo(SustenanceResource.Hunt));
            Assert.That(modifiers[0].BiomeTypeId, Is.EqualTo(BiomeType.Arid));
            Assert.That(modifiers[0].PercentValue, Is.EqualTo(ScaledPercent.FromWholePercent(20).RawValue));
        }
    }

    [Test, Description("Discovering a technology grants its extraction efficiency modifiers in the same unit of work, so the next harvest already sees them.")]
    public async Task ShouldGrantExtractionEfficiencyModifiersOnDiscoveryAsync()
    {
        const int rockAssetId = 501;

        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            seed.TechnologyExtractionEfficiencyGrants.Add(new TechnologyExtractionEfficiencyGrant
            {
                TechnologyId = _PROJECT_ID,
                AssetId = rockAssetId,
                FlatValue = ScaledPercent.FromWholePercent(95).RawValue
            });

            await seed.SaveChangesAsync();
        }

        await AddProjectAsync(directedRequired: 100);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerExtractionEfficiencyModifier modifier = await context.PlayerExtractionEfficiencyModifiers.AsNoTracking().SingleAsync(row => row.PlayerId == _PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifier.SourceId, Is.EqualTo(_PROJECT_ID));
            Assert.That(modifier.AssetId, Is.EqualTo(rockAssetId));
            Assert.That(modifier.FlatValue, Is.EqualTo(950));
        }
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
        List<ResearchHistory> rows = await context.ResearchHistories.Where(row => row.Bucket == ResearchBucket.Directed).ToListAsync();

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
        int historyCount = await context.ResearchHistories.CountAsync(row => row.Bucket == ResearchBucket.Directed);

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

    #endregion

    #region Experience drain

    [Test, Description("The drain runs before the directed spend, so points the Player earned fill the experience bucket instead of being made redundant by directed spill and thrown away.")]
    public async Task ShouldDistributeExperienceBeforeSpendingDirectedPointsAsync()
    {
        await AddProjectAsync(directedRequired: 100, experienceRequired: 50);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await AddAccrualAsync(50);
        await AddResearchStateAsync(unspentDirectedPoints: 150, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        ResearchHistory directed = await ReloadHistoryAsync();
        List<PlayerUnusedExperience> unused = await ReloadUnusedAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.ExperiencePoints, Is.EqualTo(50));
            Assert.That(directed.Spent, Is.EqualTo(100), "The experience bucket was already full, so directed points had only their own bucket to fill.");
            Assert.That(directed.Lost, Is.EqualTo(50));
            Assert.That(unused, Is.Empty, "Had the spend run first it would have filled the experience bucket by spill and left these points nowhere to go.");
        }
    }

    [Test, Description("A Technology finished by the drain alone is stamped as discovered by experience, so the Player can tell what completed it.")]
    public async Task ShouldRecordExperienceAsTheDiscoveryMethodWhenTheDrainFillsTheLastBucketAsync()
    {
        await AddProjectAsync(directedRequired: null, experienceRequired: 50);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await AddAccrualAsync(50);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.Status, Is.EqualTo(TechnologyStatus.Discovered));
            Assert.That(project.DiscoveryMethod, Is.EqualTo(TechnologyDiscoveryMethod.Experience));
            Assert.That(project.DiscoveredOnRound, Is.EqualTo(_CURRENT_ROUND_NUMBER));
        }
    }

    [Test, Description("A discovery made by the drain cascades and grants exactly as a directed one does, because the reconcilers run on the discovery rather than on the bucket that paid for it.")]
    public async Task ShouldReconcileGrantsWhenTheDrainDiscoversATechnologyAsync()
    {
        await AddSustenanceModifierGrantAsync();
        await AddProjectAsync(directedRequired: null, experienceRequired: 50);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await AddAccrualAsync(50);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology dependent = await ReloadDependentAsync();
        List<PlayerSustenanceModifier> modifiers = await ReloadModifiersAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dependent.Status, Is.EqualTo(TechnologyStatus.Available));
            Assert.That(modifiers, Has.Count.EqualTo(1));
        }
    }

    [Test, Description("The recipients are settled once, before anything is distributed, so a Technology that only became available during this drain waits until next Round rather than changing the divisor mid-split.")]
    public async Task ShouldNotFeedNewlyAvailableDependentsInTheSameRoundAsync()
    {
        await AddProjectAsync(directedRequired: null, experienceRequired: 50);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await LinkExperienceTypeAsync(_DEPENDENT_ID);
        await GiveDependentAnExperienceBucketAsync();
        await AddAccrualAsync(80);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        PlayerTechnology dependent = await ReloadDependentAsync();
        List<PlayerUnusedExperience> unused = await ReloadUnusedAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.ExperiencePoints, Is.EqualTo(50));
            Assert.That(dependent.Status, Is.EqualTo(TechnologyStatus.Available), "The cascade still ran.");
            Assert.That(dependent.ExperiencePoints, Is.Zero);
            Assert.That(unused, Has.Count.EqualTo(1));
            Assert.That(unused[0].Points, Is.EqualTo(30), "The surplus from clamping the project is wasted rather than handed to the dependent.");
        }
    }

    [Test, Description("What the Player's Forces did this Turn reaches their Technologies in the same Turn, which is the whole point of deriving the movement and settling rules at the end of one.")]
    public async Task ShouldDistributeExperienceDerivedFromThisTurnsMovementAsync()
    {
        await AddProjectAsync(directedRequired: null, experienceRequired: 50);
        await AddExperienceTypeAsync(code: _JUNGLE_CODE);
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await AddJungleTileSteppedOnThisTurnAsync();
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        ResearchHistory experience = await ReloadHistoryAsync(ResearchBucket.Experience);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.ExperiencePoints, Is.EqualTo(1), "One unimproved tile of jungle, earned and spent inside one end of Turn.");
            Assert.That(experience.Generated, Is.EqualTo(1));
        }
    }

    [Test, Description("A Technology whose experience bucket is already full is not a recipient at all, so it does not halve the share of the one that can still use the points.")]
    public async Task ShouldNotCountASpentTechnologyAsARecipientAsync()
    {
        await AddProjectAsync(directedRequired: null, experienceRequired: 500);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await LinkExperienceTypeAsync(_DEPENDENT_ID);
        await MakeDependentASpentRecipientAsync();
        await AddAccrualAsync(10);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();
        List<PlayerUnusedExperience> unused = await ReloadUnusedAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.ExperiencePoints, Is.EqualTo(10), "The only Technology that could use the points took all of them.");
            Assert.That(unused, Is.Empty, "Nothing was wasted on a divisor that could absorb nothing.");
        }
    }

    [Test, Description("Experience gets its own history row every Round even when nothing was earned, so a Round of no progress reads as a zero rather than as missing data.")]
    public async Task ShouldWriteAZeroExperienceHistoryRowWhenNothingWasAccruedAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddResearchStateAsync(unspentDirectedPoints: 100, currentTechnologyId: _PROJECT_ID);

        await _process.ProcessAsync(EndTurnContext());

        ResearchHistory experience = await ReloadHistoryAsync(ResearchBucket.Experience);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(experience.Bucket, Is.EqualTo(ResearchBucket.Experience));
            Assert.That(experience.PlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(experience.RoundNumber, Is.EqualTo(_CURRENT_ROUND_NUMBER));
            Assert.That(experience.Generated, Is.Zero);
            Assert.That(experience.Spent, Is.Zero);
            Assert.That(experience.Lost, Is.Zero);
        }
    }

    [Test, Description("Points that no Technology of the Player's consumes are banked as unused and the accrual is still emptied, so nothing is distributed twice next Round.")]
    public async Task ShouldSendPointsToUnusedWhenNoTechnologyConsumesTheTypeAsync()
    {
        await AddProjectAsync(directedRequired: 500);
        await AddExperienceTypeAsync();
        await AddAccrualAsync(40);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        List<PlayerUnusedExperience> unused = await ReloadUnusedAsync();
        List<PendingExperienceAccrual> accruals = await ReloadAccrualsAsync();
        ResearchHistory experience = await ReloadHistoryAsync(ResearchBucket.Experience);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unused, Has.Count.EqualTo(1));
            Assert.That(unused[0].ExperienceTypeId, Is.EqualTo(_EXPERIENCE_TYPE_ID));
            Assert.That(unused[0].Points, Is.EqualTo(40));
            Assert.That(accruals, Is.Empty, "The accrual table is empty between Turns.");
            Assert.That(experience.Generated, Is.EqualTo(40));
            Assert.That(experience.Lost, Is.EqualTo(40));
        }
    }

    [Test, Description("A retried end of Turn distributes once. Without the guard the second pass would pour a freshly arrived accrual into the same buckets inside one Round.")]
    public async Task ShouldDistributeOnceWhenTheEndOfTurnRunsTwiceInOneRoundAsync()
    {
        await AddProjectAsync(directedRequired: null, experienceRequired: 500);
        await AddExperienceTypeAsync();
        await LinkExperienceTypeAsync(_PROJECT_ID);
        await AddAccrualAsync(40);
        await AddResearchStateAsync(unspentDirectedPoints: 0, currentTechnologyId: null);

        await _process.ProcessAsync(EndTurnContext());

        await AddAccrualAsync(40);

        await _process.ProcessAsync(EndTurnContext());

        PlayerTechnology project = await ReloadProjectAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        int historyCount = await context.ResearchHistories.CountAsync(row => row.Bucket == ResearchBucket.Experience);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.ExperiencePoints, Is.EqualTo(40), "The second run distributed nothing.");
            Assert.That(historyCount, Is.EqualTo(1));
        }
    }

    #endregion

    #region Arrangement

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

    /// <summary>Gives the project a +20% Hunt bonus on Arid, so what discovering it grants can be read off the Player afterwards.</summary>
    private async Task AddSustenanceModifierGrantAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(new TechnologySustenanceModifierGrant
        {
            TechnologyId = _PROJECT_ID,
            Resource = SustenanceResource.Hunt,
            BiomeTypeId = BiomeType.Arid,
            FlatValue = 0,
            PercentValue = ScaledPercent.FromWholePercent(20).RawValue
        });

        await context.SaveChangesAsync();
    }

    private async Task<List<PlayerSustenanceModifier>> ReloadModifiersAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerSustenanceModifiers
            .AsNoTracking()
            .Where(modifier => modifier.PlayerId == _PLAYER_ID)
            .ToListAsync();
    }

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

    /// <summary>
    /// The Player's history row for one bucket, defaulting to Directed.
    /// </summary>
    /// <remarks>Filtered by bucket because the Turn now closes the experience books too, so a Player has more than one row per Round.</remarks>
    private async Task<ResearchHistory> ReloadHistoryAsync(ResearchBucket bucket = ResearchBucket.Directed)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.ResearchHistories.SingleAsync(row => row.PlayerId == _PLAYER_ID && row.Bucket == bucket);
    }

    #endregion

    #region Experience arrangement

    private async Task AddExperienceTypeAsync(string code = "Mining")
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.ExperienceTypes.Add(new ExperienceType
        {
            Id = _EXPERIENCE_TYPE_ID,
            Code = code,
            DisplayName = code,
            IconPath = $"/experience/{code.ToLowerInvariant()}.png",
            SortOrder = _EXPERIENCE_TYPE_ID
        });

        await context.SaveChangesAsync();
    }

    private async Task LinkExperienceTypeAsync(int technologyId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologyExperienceTypes.Add(new TechnologyExperienceType
        {
            TechnologyId = technologyId,
            ExperienceTypeId = _EXPERIENCE_TYPE_ID
        });

        await context.SaveChangesAsync();
    }

    private async Task AddAccrualAsync(int points)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PendingExperienceAccruals.Add(new PendingExperienceAccrual
        {
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            ExperienceTypeId = _EXPERIENCE_TYPE_ID,
            Points = points
        });

        await context.SaveChangesAsync();
    }

    /// <summary>Gives the dependent an experience bucket, so a test can show it is still passed over while Locked.</summary>
    private async Task GiveDependentAnExperienceBucketAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerTechnology dependent = await context.PlayerTechnologies.SingleAsync(row => row.PlayerId == _PLAYER_ID && row.TechnologyId == _DEPENDENT_ID);
        dependent.ExperienceRequired = 500;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Makes the dependent Available with its experience bucket already full.
    /// </summary>
    /// <remarks>Available rather than Locked, so what excludes it from the split is the headroom test rather than the status test.</remarks>
    private async Task MakeDependentASpentRecipientAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerTechnology dependent = await context.PlayerTechnologies.SingleAsync(row => row.PlayerId == _PLAYER_ID && row.TechnologyId == _DEPENDENT_ID);
        dependent.Status = TechnologyStatus.Available;
        dependent.ExperienceRequired = 50;
        dependent.ExperiencePoints = 50;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// One unimproved jungle tile that a Force of the Player's crossed this Turn, which the derived movement rule is worth a single point for.
    /// </summary>
    /// <remarks>No Biome or Geography row: the derivation projects <c>BiomeTypeId</c> and never materialises either navigation.</remarks>
    private async Task AddJungleTileSteppedOnThisTurnAsync()
    {
        const int tileId = 7;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMaps.Add(new CampaignMap
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            XCoord = tileId,
            YCoord = 0,
            BiomeTypeId = BiomeType.Jungle,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Biome = null!,
            Geography = null!,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });

        context.MovementHistory.Add(new MovementHistory
        {
            Id = 1,
            GameId = _GAME_ID,
            UnitInstanceId = 1,
            PlayerId = _PLAYER_ID,
            UnitTemplateId = 1,
            EventType = ForceHistoryEventType.Moved,
            FromCampaignMapTileId = tileId - 1,
            CampaignMapTileId = tileId,
            PlayerTurnId = _PLAYER_TURN_ID,
            TurnSequence = 1
        });

        await context.SaveChangesAsync();
    }

    private async Task<List<PlayerUnusedExperience>> ReloadUnusedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerUnusedExperience
            .AsNoTracking()
            .Where(row => row.PlayerId == _PLAYER_ID)
            .ToListAsync();
    }

    private async Task<List<PendingExperienceAccrual>> ReloadAccrualsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PendingExperienceAccruals
            .AsNoTracking()
            .Where(row => row.PlayerId == _PLAYER_ID)
            .ToListAsync();
    }

    #endregion
}
