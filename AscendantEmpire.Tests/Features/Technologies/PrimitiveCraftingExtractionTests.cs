using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// End-to-end tests that the real <c>PrimitiveCrafting1</c> seed entry lifts a Player's material extraction from the 5% start to the biome's whole value.
/// </summary>
/// <remarks>
/// <para>
/// The seeded entry goes through the real upsert, the discovery reconciler and the efficiency reader, and the result through the harvest's own per-tile
/// calculator, on Tundra Stone 20 - the worked example the design was settled on. How the harvest walks a territory and banks the take is pinned separately in
/// <c>HarvestSettlementTerritoryTests</c>.
/// </para>
/// <para>
/// On the in-memory tier: nothing here depends on SQL behaviour.
/// </para>
/// </remarks>
[TestFixture]
internal class PrimitiveCraftingExtractionTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _TUNDRA_STONE = 20;
    private const int _CIVILIANS = 100;

    private readonly PlayerTechnologyExtractionEfficiencyService _reconciler = new(NullLogger<PlayerTechnologyExtractionEfficiencyService>.Instance);

    private readonly PlayerExtractionEfficiencyReader _reader = new();

    private Dictionary<MaterialResource, int> _assetIdsByResource = [];

    private int _playerId;
    private int _primitiveCraftingId;

    #region Setup

    [SetUp]
    public async Task SeedCatalogueAndPlayerAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await SeedExperienceTypesAsync(context);
        _assetIdsByResource = await TechnologySeedTestAssets.SeedMaterialAssetsAsync(context);

        UpsertTechnologyDto primitiveCrafting = TechnologySeedData.Build().Single(technology => technology.Code == "PrimitiveCrafting1");

        ServiceResultBase result = await new UpsertTechnologiesHandler(new NullLogger<UpsertTechnologiesHandler>(), context)
            .Handle(new UpsertTechnologiesRequest([primitiveCrafting]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        var player = new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Tester",
            FactionName = "Testers",
            PeopleName = "Tester People",
            Demonym = "Testerian",
            Language = "Testerish",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active
        };

        context.Players.Add(player);
        await context.SaveChangesAsync();

        _playerId = player.Id;
        _primitiveCraftingId = await context.Technologies.Where(technology => technology.Code == "PrimitiveCrafting1").Select(technology => technology.Id).SingleAsync();
    }

    #endregion

    #region PrimitiveCrafting1

    [Test, Description("Before PrimitiveCrafting1 a Settlement on Tundra takes 1 Stone of the 20 the land holds, which is the barely-gathering phase the design wants.")]
    public async Task ShouldBankOneStoneWhenPrimitiveCrafting1IsNotDiscovered()
    {
        await SetStatusAsync(TechnologyStatus.Available);

        ScaledAmount take = await TakeAfterReconcileAsync(MaterialResource.Stone);

        Assert.That(take, Is.EqualTo(ScaledAmount.FromWhole(1)));
    }

    [Test, Description("Discovering PrimitiveCrafting1 lifts Tundra Stone to the full 20, which is the acceleration the technology exists for.")]
    public async Task ShouldBankTwentyStoneWhenPrimitiveCrafting1IsDiscovered()
    {
        await SetStatusAsync(TechnologyStatus.Discovered);

        ScaledAmount take = await TakeAfterReconcileAsync(MaterialResource.Stone);

        Assert.That(take, Is.EqualTo(ScaledAmount.FromWhole(_TUNDRA_STONE)));
    }

    [TestCase(MaterialResource.Wood, Description = "PrimitiveCrafting1 lifts Wood as well as Stone.")]
    [TestCase(MaterialResource.Bone, Description = "PrimitiveCrafting1 lifts Bone as well as Stone.")]
    public async Task ShouldRaiseWoodAndBoneTooWhenPrimitiveCrafting1IsDiscovered(MaterialResource resource)
    {
        await SetStatusAsync(TechnologyStatus.Discovered);

        ScaledAmount take = await TakeAfterReconcileAsync(resource);

        Assert.That(take, Is.EqualTo(ScaledAmount.FromWhole(_TUNDRA_STONE)));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Reconciles the Player, reads their efficiencies, and works out what one Tundra tile with a base of 20 yields for the material.
    /// </summary>
    private async Task<ScaledAmount> TakeAfterReconcileAsync(MaterialResource resource)
    {
        await using (AscendantEmpireContext reconcile = await DbFactory.CreateDbContextAsync())
        {
            await _reconciler.ReconcileAsync(reconcile, _GAME_ID, _playerId, CancellationToken.None);
            await reconcile.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerExtractionEfficiencies efficiencies = await _reader.GetEfficienciesAsync(context, _playerId);

        ScaledPercent efficiency = efficiencies.GetEfficiency(_assetIdsByResource[resource], SettlementHarvestCalculator.STARTING_MATERIAL_EFFICIENCY[resource]);

        return SettlementHarvestCalculator.CalculateMaterialTake(ScaledAmount.FromWhole(_TUNDRA_STONE), efficiency, _CIVILIANS);
    }

    private async Task SetStatusAsync(TechnologyStatus status)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerTechnologies.Add(new PlayerTechnology
        {
            PlayerId = _playerId,
            TechnologyId = _primitiveCraftingId,
            GameId = _GAME_ID,
            Status = status,
            ExperienceRequired = 3,
            DirectedPoints = 0,
            ExperiencePoints = 0,
            DiffusionPoints = 0
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedExperienceTypesAsync(AscendantEmpireContext context)
    {
        ServiceResultBase result = await new UpsertExperienceTypesHandler(new NullLogger<UpsertExperienceTypesHandler>(), context)
            .Handle(new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build()), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
    }

    #endregion
}
