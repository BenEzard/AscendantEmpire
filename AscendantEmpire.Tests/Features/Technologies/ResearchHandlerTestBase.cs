using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// A game, one Player, a campaign turn queue and a research state, with helpers for standing up catalogue and per-Player rows.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the read and the write fixture because both need the same scenery, and because a per-Player row written one way in one fixture and another way
/// in the other is how the two drift apart. Catalogue rows go in through the real upsert handler rather than being hand-built, so a change to how the
/// catalogue is stored shows up in these tests rather than being papered over.
/// </para>
/// <para>
/// In-memory throughout: everything here is ordinary EF - LINQ, Add, SaveChanges. The database's check constraints do not exist on this provider, so the
/// status stamps these helpers write are the ones a real unlock writes rather than whatever would satisfy an assertion.
/// </para>
/// </remarks>
internal abstract class ResearchHandlerTestBase : InMemoryTestBase
{
    protected const int GAME_ID = 1;

    /// <summary>
    /// The Round the fixture's campaign queue starts on, so a test asserting against a stamped Round has a figure to name.
    /// </summary>
    protected const int FIRST_ROUND = 1;

    protected AscendantEmpireContext Context { get; private set; } = null!;

    protected int PlayerId { get; private set; }

    [SetUp]
    public async Task SetUpResearchScenery()
    {
        Context = await DbFactory.CreateDbContextAsync();

        Context.GameIndex.Add(new GameIndex
        {
            Id = GAME_ID,
            GameName = "Fixture",
            CampaignMapIndexId = 1,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.AwaitingPlayers,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30
        });

        // The campaign lane is where the game clock lives, and both handlers read the Round off it to stamp an unlock with.
        Context.TurnQueues.Add(new TurnQueue
        {
            GameId = GAME_ID,
            QueueType = TurnQueueType.Campaign,
            CurrentRoundNumber = FIRST_ROUND,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Context.SaveChangesAsync();

        PlayerId = await AddPlayerAsync("Fixture");
        await SeedResearchStateAsync(PlayerId);
    }

    [TearDown]
    public void DisposeResearchScenery()
    {
        Context?.Dispose();
    }

    // --- Scenery ----------------------------------------------------------------------

    /// <summary>
    /// Adds a Player to the fixture's game and returns their id. No research state - call <see cref="SeedResearchStateAsync"/> for that.
    /// </summary>
    protected async Task<int> AddPlayerAsync(string factionName)
    {
        var player = new Player
        {
            GameId = GAME_ID,
            RulerTitle = "Chief",
            RulerName = factionName,
            FactionName = factionName,
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            RulerPortraitPath = "/portraits/ruler_01.png"
        };

        Context.Players.Add(player);
        await Context.SaveChangesAsync();

        return player.Id;
    }

    /// <summary>
    /// Opens a Player's research state, as game setup does - no project, nothing banked.
    /// </summary>
    protected async Task SeedResearchStateAsync(int playerId)
    {
        Context.PlayerResearchStates.Add(new PlayerResearchState
        {
            PlayerId = playerId,
            GameId = GAME_ID
        });

        await Context.SaveChangesAsync();
    }

    protected async Task SetCurrentRoundAsync(int roundNumber)
    {
        TurnQueue queue = await Context.TurnQueues.SingleAsync(candidate => candidate.GameId == GAME_ID && candidate.QueueType == TurnQueueType.Campaign);

        queue.CurrentRoundNumber = roundNumber;

        await Context.SaveChangesAsync();
    }

    // --- Catalogue --------------------------------------------------------------------

    /// <summary>
    /// A directed-only technology with no acquisition cost - the shape every opening technology in the real catalogue has.
    /// </summary>
    protected static UpsertTechnologyDto Technology(string code)
    {
        return new UpsertTechnologyDto
        {
            Code = code,
            DisplayName = code,
            Era = TechnologyEra.Stone,
            DirectedRequired = 100
        };
    }

    protected static UpsertTechnologyPrerequisiteDto Mandatory(string code)
    {
        return new UpsertTechnologyPrerequisiteDto { PrerequisiteTechnologyCode = code };
    }

    protected async Task SeedCatalogueAsync(IReadOnlyCollection<UpsertTechnologyDto> technologies)
    {
        var handler = new UpsertTechnologiesHandler(NullLogger<UpsertTechnologiesHandler>.Instance, Context);

        ServiceResultBase result = await handler.Handle(new UpsertTechnologiesRequest(technologies), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }

    /// <summary>
    /// Adds one Asset for an acquisition cost to name, with the meta-category and category its own mapping requires.
    /// </summary>
    protected async Task SeedAssetAsync(string code, string displayName)
    {
        var metaCategory = new AssetMetaCategory
        {
            Code = "Probe",
            DisplayName = "Probe",
            SortOrder = 10,
            IconPath = string.Empty
        };

        Context.Assets.Add(new Asset
        {
            Code = code,
            DisplayName = displayName,
            IconPath = string.Empty,
            IsGradedAsset = false,
            SortOrder = 10,
            MetaCategory = metaCategory,
            Category = new AssetCategory
            {
                Code = "Probe.Category",
                DisplayName = "Probe Category",
                SortOrder = 10,
                CategoryType = AssetCategoryType.StandaloneAssets,
                MetaCategory = metaCategory
            }
        });

        await Context.SaveChangesAsync();
    }

    protected static UpsertTechnologyAcquisitionCostDto Cost(string assetCode, int quantity)
    {
        return new UpsertTechnologyAcquisitionCostDto { AssetCode = assetCode, Quantity = quantity };
    }

    // --- Per-Player rows --------------------------------------------------------------

    /// <summary>
    /// Writes a Locked row for every catalogue technology, as game setup's requirement generator would.
    /// </summary>
    protected async Task SeedPlayerRowsAsync(int? playerId = null)
    {
        int targetPlayerId = playerId ?? PlayerId;

        int[] alreadyHeld = await Context.PlayerTechnologies
            .Where(row => row.PlayerId == targetPlayerId)
            .Select(row => row.TechnologyId)
            .ToArrayAsync();

        List<Technology> catalogue = await Context.Technologies
            .Where(technology => !alreadyHeld.Contains(technology.Id))
            .ToListAsync();

        Context.PlayerTechnologies.AddRange(catalogue.Select(technology => new PlayerTechnology
        {
            PlayerId = targetPlayerId,
            TechnologyId = technology.Id,
            GameId = GAME_ID,
            Status = TechnologyStatus.Locked,
            DirectedRequired = technology.DirectedRequired,
            ExperienceRequired = technology.ExperienceRequired,
            DiffusionRequired = technology.DiffusionRequired
        }));

        await Context.SaveChangesAsync();
    }

    protected async Task SetStatusAsync(
        string code,
        TechnologyStatus status,
        int? acquisitionPaidOnRound = null,
        int? discoveredOnRound = null,
        int? playerId = null)
    {
        PlayerTechnology row = await RowAsync(code, playerId);

        row.Status = status;
        row.AcquisitionPaidOnRound = acquisitionPaidOnRound;
        row.DiscoveredOnRound = discoveredOnRound;

        await Context.SaveChangesAsync();
    }

    protected async Task SetRequirementsAsync(
        string code,
        int? directedRequired = null,
        int? experienceRequired = null,
        int? diffusionRequired = null)
    {
        PlayerTechnology row = await RowAsync(code);

        row.DirectedRequired = directedRequired;
        row.ExperienceRequired = experienceRequired;
        row.DiffusionRequired = diffusionRequired;

        await Context.SaveChangesAsync();
    }

    protected async Task SetPointsAsync(
        string code,
        int directedPoints = 0,
        int experiencePoints = 0,
        int diffusionPoints = 0)
    {
        PlayerTechnology row = await RowAsync(code);

        row.DirectedPoints = directedPoints;
        row.ExperiencePoints = experiencePoints;
        row.DiffusionPoints = diffusionPoints;

        await Context.SaveChangesAsync();
    }

    // --- Research state ---------------------------------------------------------------

    protected async Task SetCurrentProjectAsync(string? code)
    {
        PlayerResearchState state = await ResearchStateAsync();

        state.CurrentTechnologyId = code is null ? null : await TechnologyIdAsync(code);

        await Context.SaveChangesAsync();
    }

    protected async Task SetUnspentPointsAsync(int directed = 0, int diffusion = 0)
    {
        PlayerResearchState state = await ResearchStateAsync();

        state.UnspentDirectedPoints = directed;
        state.UnspentDiffusionPoints = diffusion;

        await Context.SaveChangesAsync();
    }

    protected async Task<PlayerResearchState> ResearchStateAsync(int? playerId = null)
    {
        DetachEverything();

        return await Context.PlayerResearchStates.SingleAsync(state => state.PlayerId == (playerId ?? PlayerId));
    }

    // --- Lookups ----------------------------------------------------------------------

    protected async Task<int> TechnologyIdAsync(string code)
    {
        return await Context.Technologies.Where(technology => technology.Code == code).Select(technology => technology.Id).SingleAsync();
    }

    protected async Task<PlayerTechnology> RowAsync(string code, int? playerId = null)
    {
        int technologyId = await TechnologyIdAsync(code);

        DetachEverything();

        return await Context.PlayerTechnologies.SingleAsync(row => row.PlayerId == (playerId ?? PlayerId) && row.TechnologyId == technologyId);
    }

    /// <summary>
    /// Empties this fixture's change tracker, so the next read comes from the store rather than from what this context last had in hand.
    /// </summary>
    /// <remarks>
    /// <b>Load-bearing, not tidiness.</b> A handler under test writes through its own context from <c>DbFactory</c>, while the fixture holds one long-lived
    /// context of its own. EF resolves identity from the change tracker first, so without this a verification read would hand back the stale instance this
    /// fixture itself seeded - an unlock written by the handler would look as though it had never happened.
    /// </remarks>
    private void DetachEverything()
    {
        Context.ChangeTracker.Clear();
    }
}
