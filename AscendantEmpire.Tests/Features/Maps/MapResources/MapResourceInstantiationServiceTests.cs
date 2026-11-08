using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins the service that reads a map's MapResource placements and writes one Game's instances and their child rows.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier for two reasons the lighter tiers cannot cover: the instances carry a foreign key to <c>game.CampaignMap</c>, whose rows game
/// setup writes with raw SQL, and the writes themselves go through <see cref="System.Data.SqlClient"/> bulk copy. The rolls are pinned without a database in
/// <see cref="MapResourceInstantiationRulesTests"/>; what is pinned here is that the rows land where they belong.
/// </remarks>
internal sealed class MapResourceInstantiationServiceTests : GameSqlServerTestBase
{
    private const int _WIDTH = 3;
    private const int _HEIGHT = 2;
    private const long _INSTANTIATION_SEED = 4242;

    // The ranges and the one fixed amount MapResourceTestSeed's catalogue carries. Restated so a value assertion says what it is checking against rather than
    // reaching back into the seed, and so a change to the seed's figures shows up as a failing assertion rather than a silently weaker one.
    private const long _SCENARIO_AMOUNT = 10_000_000;
    private const long _BERRY_QUANTITY_MIN = 200_000;
    private const long _BERRY_QUANTITY_MAX = 400_000;
    private const int _BERRY_SPREAD_MIN = 2;
    private const int _BERRY_SPREAD_MAX = 5;
    private const decimal _BERRY_FUZZY_MIN = 0.1m;
    private const decimal _BERRY_FUZZY_MAX = 0.3m;
    private const decimal _WOOD_FUZZY_MIN = 0.1m;
    private const decimal _WOOD_FUZZY_MAX = 0.2m;
    private const long _COAL_LAYER_QUANTITY_MIN = 1_000_000;
    private const long _COAL_LAYER_QUANTITY_MAX = 5_000_000;
    private const decimal _COAL_LAYER_FUZZY_MIN = 0.1m;
    private const decimal _COAL_LAYER_FUZZY_MAX = 0.2m;

    private MapResourceTestCatalogue _catalogue = null!;

    /// <summary>
    /// A Game whose map is wholly Forest, so every placement's tile is covered by the test catalogue's habitats.
    /// </summary>
    /// <remarks>
    /// The Biome is repainted before setup runs, so the Game's own tiles carry it too rather than diverging from the gallery they were copied from.
    /// </remarks>
    private async Task<int> SeedForestGameAsync()
    {
        await SeedGameAsync(mapWidth: _WIDTH, mapHeight: _HEIGHT, numberOfPlayers: 2);

        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            _catalogue = await MapResourceTestSeed.SeedCatalogueAsync(seed);

            await seed.CampaignMapGallery
                .Where(tile => tile.CampaignMapGalleryIndexId == _galleryIndexId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(tile => tile.BiomeTypeId, BiomeType.Forest)
                    .SetProperty(tile => tile.CampaignMapGeographyTypeId, CampaignMapGeographyType.Flat));
        }

        ServiceResultBase.ServiceResult<int> setup = await _service.SetupGameAsync(
            new SetupGameRequest(_galleryIndexId, "Instantiation Game", 2, 50, GenerationSeed: _INSTANTIATION_SEED),
            CancellationToken.None);

        Assert.That(setup.HasSucceeded, Is.True, () => string.Join("; ", setup.Errors));

        return setup.SuccessValue;
    }

    /// <summary>
    /// Places a Berry and a Wood on the gallery tile at (0, 0) and a Coal at (1, 0): one of each kind, and one tile filled to both its slots.
    /// </summary>
    /// <remarks>
    /// Placed after setup rather than before, so that this fixture pins the service alone and is unaffected by setup later calling it itself.
    /// </remarks>
    private async Task SeedPlacementsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Dictionary<(int X, int Y), int> galleryTileIds = await context.CampaignMapGallery
            .Where(tile => tile.CampaignMapGalleryIndexId == _galleryIndexId)
            .ToDictionaryAsync(tile => (tile.XCoord, tile.YCoord), tile => tile.CampaignMapGalleryId);

        context.CampaignMapGalleryResources.AddRange(
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(0, 0)], MapResourceTypeId = _catalogue.BerryTypeId, Slot = 1 },
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(0, 0)], MapResourceTypeId = _catalogue.WoodTypeId, Slot = 2 },
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(1, 0)], MapResourceTypeId = _catalogue.CoalTypeId, Slot = 1 });

        await context.SaveChangesAsync();
    }

    private async Task<ServiceResultBase.ServiceResult<int>> InstantiateAsync(int gameId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        GameIndex game = await context.GameIndex.SingleAsync(g => g.Id == gameId);

        var service = new MapResourceInstantiationService(NullLogger<MapResourceInstantiationService>.Instance, DbFactory);

        return await service.InstantiateForGameAsync(
            gameId,
            _galleryIndexId,
            _INSTANTIATION_SEED,
            game.CampaignMapOffsetId,
            game.MapWidth,
            CancellationToken.None);
    }

    [Test, Description("Every gallery placement becomes an instance on the matching campaign-map tile, which is the whole point of the step.")]
    public async Task ShouldCreateOneInstancePerPlacementOnMatchingTileAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        ServiceResultBase.ServiceResult<int> result = await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameIndex game = await verify.GameIndex.SingleAsync(g => g.Id == gameId);

        List<MapResourceInstance> instances = await verify.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .ToListAsync();

        int tileZeroZero = game.CampaignMapOffsetId;
        int tileOneZero = game.CampaignMapOffsetId + 1;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(3), "Three placements, three instances.");
            Assert.That(instances, Has.Count.EqualTo(3), "The rows must actually be in the database, not merely counted.");

            Assert.That(
                instances.Where(instance => instance.CampaignMapId == tileZeroZero).Select(instance => instance.Slot),
                Is.EquivalentTo(new byte[] { 1, 2 }),
                "The tile at (0, 0) carried placements in both slots.");

            Assert.That(
                instances.Single(instance => instance.CampaignMapId == tileOneZero).MapResourceTypeId,
                Is.EqualTo(_catalogue.CoalTypeId),
                "The Coal placement was on the tile at (1, 0).");
        }
    }

    [Test, Description("Instances setup creates are stamped on Round 0 and active, marking them as the Game's original resources rather than spread ones.")]
    public async Task ShouldCreateInstancesOnRoundZeroAndNotDormantAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<MapResourceInstance> instances = await verify.MapResourceInstances.Where(instance => instance.GameId == gameId).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instances.Select(instance => instance.CreatedOnRound), Has.All.Zero, "Round 0 is what distinguishes an original instance from a spread one.");
            Assert.That(instances.Select(instance => instance.DormantSinceRound), Has.All.Null, "Nothing is exhausted before the Game begins.");
        }
    }

    [Test, Description("A Distribution's amounts land as child rows against their own instance, so felling one tile cannot affect another.")]
    public async Task ShouldWriteAmountRowsAgainstTheirOwnInstanceAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        MapResourceInstance wood = await verify.MapResourceInstances
            .SingleAsync(instance => instance.GameId == gameId && instance.MapResourceTypeId == _catalogue.WoodTypeId);

        List<MapResourceInstanceAmount> amounts = await verify.MapResourceInstanceAmounts
            .Where(amount => amount.GameId == gameId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wood.MapResourceDistributionScenarioId, Is.EqualTo(_catalogue.ScenarioId), "The only scenario weighted for Forest is the catalogue's.");
            Assert.That(wood.Quantity, Is.Null, "A Distribution holds its quantity in its amount rows.");
            Assert.That(amounts, Has.Count.EqualTo(1), "The scenario holds one Asset.");
            Assert.That(amounts.Single().MapResourceInstanceId, Is.EqualTo(wood.Id), "An amount attached to the wrong instance is silently wrong data.");
            Assert.That(amounts.Single().AssetId, Is.EqualTo(_catalogue.AssetId), "The Asset is copied from the scenario.");
            Assert.That(amounts.Single().Amount, Is.EqualTo(_SCENARIO_AMOUNT), "The quantity is copied from the scenario, not rolled.");
            Assert.That(
                wood.EstimationFuzzyFactor,
                Is.InRange(_WOOD_FUZZY_MIN, _WOOD_FUZZY_MAX),
                "A Distribution still carries its own estimation error, and it has to reach the column rather than being dropped on the way.");
        }
    }

    [Test, Description("A stratified instance's layers land as child rows against their own instance, carrying their layer numbers.")]
    public async Task ShouldWriteLayerRowsAgainstTheirOwnInstanceAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        MapResourceInstance coal = await verify.MapResourceInstances
            .SingleAsync(instance => instance.GameId == gameId && instance.MapResourceTypeId == _catalogue.CoalTypeId);

        List<MapResourceInstanceLayer> layers = await verify.MapResourceInstanceLayers
            .Where(layer => layer.GameId == gameId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(layers, Has.Count.EqualTo(1), "The catalogue's Coal defines one layer.");
            Assert.That(layers.Single().MapResourceInstanceId, Is.EqualTo(coal.Id), "A layer attached to the wrong instance is silently wrong data.");
            Assert.That(layers.Single().LayerNumber, Is.EqualTo(1), "The layer keeps its depth.");
            Assert.That(coal.Quantity, Is.Null, "A stratified instance holds its quantity in its layers.");
            Assert.That(coal.EstimationFuzzyFactor, Is.Null, "A stratified instance's estimation error lives on each layer.");

            Assert.That(
                layers.Single().EstimationFuzzyFactor,
                Is.InRange(_COAL_LAYER_FUZZY_MIN, _COAL_LAYER_FUZZY_MAX),
                "The layer's rolled estimation error has to reach its column; a zero or a null would be a value the rules never produced.");

            // The layer rolls empty one time in four, and the two outcomes mean different rows. Asserted together so neither can pass by being the other.
            if (layers.Single().AssetId == null)
            {
                Assert.That(layers.Single().Quantity, Is.Zero, "A layer that rolled empty holds nothing.");
            }
            else
            {
                Assert.That(layers.Single().AssetId, Is.EqualTo(_catalogue.AssetId), "The Asset is copied from the layer template.");
                Assert.That(layers.Single().Quantity, Is.InRange(_COAL_LAYER_QUANTITY_MIN, _COAL_LAYER_QUANTITY_MAX), "Its quantity is rolled within the template's range.");
            }
        }
    }

    [Test, Description("A single-quantity instance's rolled quantity, spread limit and estimation error all reach their columns rather than landing null.")]
    public async Task ShouldWriteRolledValuesOnSingleQuantityInstanceAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        MapResourceInstance berry = await verify.MapResourceInstances
            .SingleAsync(instance => instance.GameId == gameId && instance.MapResourceTypeId == _catalogue.BerryTypeId);

        int childAmounts = await verify.MapResourceInstanceAmounts.CountAsync(amount => amount.MapResourceInstanceId == berry.Id);
        int childLayers = await verify.MapResourceInstanceLayers.CountAsync(layer => layer.MapResourceInstanceId == berry.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(berry.Quantity, Is.InRange(_BERRY_QUANTITY_MIN, _BERRY_QUANTITY_MAX), "The quantity is rolled within the habitat's range.");
            Assert.That(berry.SpreadLimit, Is.InRange(_BERRY_SPREAD_MIN, _BERRY_SPREAD_MAX), "The spread limit is rolled within the habitat's range.");
            Assert.That(berry.EstimationFuzzyFactor, Is.InRange(_BERRY_FUZZY_MIN, _BERRY_FUZZY_MAX), "The estimation error is rolled from the type's range.");
            Assert.That(childAmounts, Is.Zero, "Only a Distribution holds amount rows.");
            Assert.That(childLayers, Is.Zero, "Only a stratified instance holds layer rows.");
        }
    }

    [Test, Description("Children attach only to this Game's instances, so a database holding several Games never cross-wires them.")]
    public async Task ShouldAttachChildrenOnlyToThisGamesInstancesAsync()
    {
        int firstGameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        ServiceResultBase.ServiceResult<int> first = await InstantiateAsync(firstGameId);

        Assert.That(first.HasSucceeded, Is.True, () => string.Join("; ", first.Errors));

        // A second Game from the same map, set up while the first Game's rows are already there. Its own setup instantiates its placements, since they exist
        // before it runs; calling the service again here would collide with UX_MapResourceInstance_Tile_Slot on the rows setup had just written.
        ServiceResultBase.ServiceResult<int> secondSetup = await _service.SetupGameAsync(
            new SetupGameRequest(_galleryIndexId, "Second Instantiation Game", 2, 50, GenerationSeed: _INSTANTIATION_SEED + 1),
            CancellationToken.None);

        Assert.That(secondSetup.HasSucceeded, Is.True, () => string.Join("; ", secondSetup.Errors));

        int secondGameId = secondSetup.SuccessValue;

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<int> secondGameInstanceIds = await verify.MapResourceInstances
            .Where(instance => instance.GameId == secondGameId)
            .Select(instance => instance.Id)
            .ToListAsync();

        List<MapResourceInstanceAmount> secondGameAmounts = await verify.MapResourceInstanceAmounts
            .Where(amount => amount.GameId == secondGameId)
            .ToListAsync();

        List<MapResourceInstanceLayer> secondGameLayers = await verify.MapResourceInstanceLayers
            .Where(layer => layer.GameId == secondGameId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondGameInstanceIds, Has.Count.EqualTo(3), "The second Game gets its own three instances.");

            // Guarded, because a subset assertion over nothing holds whatever the service did.
            Assert.That(secondGameAmounts, Is.Not.Empty, "The second Game's Distribution must have written its amount row.");
            Assert.That(secondGameLayers, Is.Not.Empty, "The second Game's stratified instance must have written its layer row.");

            Assert.That(
                secondGameAmounts.Select(amount => amount.MapResourceInstanceId),
                Is.SubsetOf(secondGameInstanceIds),
                "An amount pointing at the first Game's instance would mean the read-back was not filtered by Game.");
            Assert.That(
                secondGameLayers.Select(layer => layer.MapResourceInstanceId),
                Is.SubsetOf(secondGameInstanceIds),
                "A layer pointing at the first Game's instance would mean the same.");
        }
    }

    [Test, Description("Rolling back a Game's MapResource rows removes all three tables' rows for that Game and leaves another Game's standing.")]
    public async Task ShouldRemoveOnlyThisGamesRowsWhenRollingBackAsync()
    {
        int keptGameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        ServiceResultBase.ServiceResult<int> kept = await InstantiateAsync(keptGameId);

        Assert.That(kept.HasSucceeded, Is.True, () => string.Join("; ", kept.Errors));

        // The second Game's setup instantiates it, the placements already being there, so there are two Games' rows to tell apart.
        ServiceResultBase.ServiceResult<int> discardedSetup = await _service.SetupGameAsync(
            new SetupGameRequest(_galleryIndexId, "Rolled Back Game", 2, 50, GenerationSeed: _INSTANTIATION_SEED + 2),
            CancellationToken.None);

        Assert.That(discardedSetup.HasSucceeded, Is.True, () => string.Join("; ", discardedSetup.Errors));

        int discardedGameId = discardedSetup.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(
            await context.MapResourceInstances.CountAsync(instance => instance.GameId == discardedGameId),
            Is.EqualTo(3),
            "Nothing is being pinned if the Game had no rows to remove.");

        await MapResourceInstantiationService.RollBackGameMapResourcesAsync(context, discardedGameId, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.MapResourceInstances.CountAsync(i => i.GameId == discardedGameId), Is.Zero, "The instances go.");
            Assert.That(await context.MapResourceInstanceAmounts.CountAsync(a => a.GameId == discardedGameId), Is.Zero, "So do their amount rows.");
            Assert.That(await context.MapResourceInstanceLayers.CountAsync(l => l.GameId == discardedGameId), Is.Zero, "So do their layer rows.");

            Assert.That(await context.MapResourceInstances.CountAsync(i => i.GameId == keptGameId), Is.EqualTo(3), "The other Game's instances stand.");
            Assert.That(await context.MapResourceInstanceAmounts.CountAsync(a => a.GameId == keptGameId), Is.Positive, "And so do its children.");
            Assert.That(await context.MapResourceInstanceLayers.CountAsync(l => l.GameId == keptGameId), Is.Positive, "Both kinds of them.");
        }
    }

    [Test, Description("A map with no placements creates nothing and still succeeds, since a map need not carry MapResources at all.")]
    public async Task ShouldSucceedWithoutCreatingAnythingWhenMapHasNoPlacementsAsync()
    {
        int gameId = await SeedForestGameAsync();

        ServiceResultBase.ServiceResult<int> result = await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.Zero, "Nothing was placed, so nothing is created.");
            Assert.That(await verify.MapResourceInstances.CountAsync(instance => instance.GameId == gameId), Is.Zero, "No rows should be written at all.");
        }
    }

    [Test, Description("A placement no habitat covers is skipped while the rest are created, so one stale placement cannot block a Game.")]
    public async Task ShouldSkipRefusedPlacementAndCreateTheRestAsync()
    {
        int gameId = await SeedForestGameAsync();
        await SeedPlacementsAsync();

        // Repaint the Coal tile to a Biome the catalogue has no habitat for, exactly as repainting a Biome after placing would leave it.
        await using (AscendantEmpireContext repaint = await DbFactory.CreateDbContextAsync())
        {
            await repaint.CampaignMapGallery
                .Where(tile => tile.CampaignMapGalleryIndexId == _galleryIndexId && tile.XCoord == 1 && tile.YCoord == 0)
                .ExecuteUpdateAsync(set => set.SetProperty(tile => tile.BiomeTypeId, BiomeType.Grassland));
        }

        ServiceResultBase.ServiceResult<int> result = await InstantiateAsync(gameId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<MapResourceInstance> instances = await verify.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(instances, Has.Count.EqualTo(2), "The two Forest placements stand; the Grassland one is skipped.");
            Assert.That(
                instances.Select(instance => instance.MapResourceTypeId),
                Has.None.EqualTo(_catalogue.CoalTypeId),
                "Coal has no Grassland habitat, so it cannot exist on that tile.");
        }
    }
}
