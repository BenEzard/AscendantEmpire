using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Tests.Features.Maps.MapResources;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Integration tests for the MapResource step of game setup: a new Game holds an instance for every placement its map was authored with, rolled from the
/// Game's own seed, and a Game whose MapResources cannot be created is failed rather than left holding none.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> generates the campaign map with raw SQL. The rolls themselves are pinned without a database in
/// <c>MapResourceInstantiationRulesTests</c>; what is pinned here is that setup runs the step at all, and against the Game's own tiles.
/// </remarks>
internal sealed class MapResourceInstantiationSetupTests : GameSqlServerTestBase
{
    private const int _WIDTH = 3;
    private const int _HEIGHT = 2;

    private MapResourceTestCatalogue _catalogue = null!;

    /// <summary>
    /// A wholly Forest gallery map carrying one placement of each kind, ready for setup to create a Game from.
    /// </summary>
    /// <remarks>
    /// The placements go on before setup runs, which is the real order: setup reads what the map was authored with.
    /// </remarks>
    private async Task SeedForestMapWithPlacementsAsync()
    {
        await SeedGameAsync(mapWidth: _WIDTH, mapHeight: _HEIGHT, numberOfPlayers: 2);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _catalogue = await MapResourceTestSeed.SeedCatalogueAsync(context);

        await context.CampaignMapGallery
            .Where(tile => tile.CampaignMapGalleryIndexId == _galleryIndexId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(tile => tile.BiomeTypeId, BiomeType.Forest)
                .SetProperty(tile => tile.CampaignMapGeographyTypeId, CampaignMapGeographyType.Flat));

        Dictionary<(int X, int Y), int> galleryTileIds = await context.CampaignMapGallery
            .Where(tile => tile.CampaignMapGalleryIndexId == _galleryIndexId)
            .ToDictionaryAsync(tile => (tile.XCoord, tile.YCoord), tile => tile.CampaignMapGalleryId);

        context.CampaignMapGalleryResources.AddRange(
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(0, 0)], MapResourceTypeId = _catalogue.BerryTypeId, Slot = 1 },
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(0, 0)], MapResourceTypeId = _catalogue.WoodTypeId, Slot = 2 },
            new CampaignMapGalleryResource { CampaignMapGalleryId = galleryTileIds[(1, 0)], MapResourceTypeId = _catalogue.CoalTypeId, Slot = 1 });

        await context.SaveChangesAsync();
    }

    private SetupGameRequest BuildRequest(string gameName, long? seed)
    {
        return new SetupGameRequest(_galleryIndexId, gameName, NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: seed);
    }

    /// <summary>
    /// Every rolled value of a Game's instances, keyed by the tile's position in the map and its slot.
    /// </summary>
    /// <remarks>
    /// Keyed on the position rather than the tile id, because two Games from one map sit in different blocks of ids. Identity is deliberately absent: what
    /// two Games from one seed must share is what was rolled, not which rows hold it.
    /// </remarks>
    private static async Task<Dictionary<(int Position, byte Slot), string>> ReadRolledValuesAsync(
        AscendantEmpireContext context,
        int gameId)
    {
        GameIndex game = await context.GameIndex.SingleAsync(index => index.Id == gameId);

        List<MapResourceInstance> instances = await context.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .ToListAsync();

        return instances.ToDictionary(
            instance => (instance.CampaignMapId - game.CampaignMapOffsetId, instance.Slot),
            instance => string.Join(
                '|',
                instance.MapResourceTypeId,
                instance.Quantity,
                instance.SpreadLimit,
                instance.EstimationFuzzyFactor,
                instance.MapResourceDistributionScenarioId));
    }

    [Test, Description("Setting up a Game creates its MapResource instances, so a new Game's tiles hold the resources its map was authored with.")]
    public async Task ShouldCreateMapResourceInstancesWhenGameIsSetUpAsync()
    {
        await SeedForestMapWithPlacementsAsync();

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest("Resource Setup Game", seed: 777), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<MapResourceInstance> instances = await verify.MapResourceInstances
            .Where(instance => instance.GameId == result.SuccessValue)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instances, Has.Count.EqualTo(3), "One instance per placement on the map.");
            Assert.That(
                instances.Select(instance => instance.MapResourceTypeId),
                Is.EquivalentTo(new[] { _catalogue.BerryTypeId, _catalogue.WoodTypeId, _catalogue.CoalTypeId }),
                "All three kinds must be created, not just the simple one.");
            Assert.That(await verify.MapResourceInstanceAmounts.CountAsync(a => a.GameId == result.SuccessValue), Is.EqualTo(1), "The Distribution's amounts come too.");
            Assert.That(await verify.MapResourceInstanceLayers.CountAsync(l => l.GameId == result.SuccessValue), Is.EqualTo(1), "The stratified type's layers come too.");
        }
    }

    [Test, Description("Instances are created against this Game's own tile ids, which is what keeps two Games on one map independent.")]
    public async Task ShouldCreateInstancesAgainstThisGamesTileIdsAsync()
    {
        await SeedForestMapWithPlacementsAsync();

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest("Tile Id Game", seed: 778), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        int gameId = result.SuccessValue;

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<int> gameTileIds = await verify.CampaignMaps
            .Where(tile => tile.GameId == gameId)
            .Select(tile => tile.CampaignMapId)
            .ToListAsync();

        // Distinct, because a tile legitimately holds one instance per slot and Is.SubsetOf would otherwise reject the repeated tile id.
        List<int> instanceTileIds = await verify.MapResourceInstances
            .Where(instance => instance.GameId == gameId)
            .Select(instance => instance.CampaignMapId)
            .Distinct()
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instanceTileIds, Is.Not.Empty, "A map with placements must produce instances, or the subset assertion below proves nothing.");
            Assert.That(instanceTileIds, Is.SubsetOf(gameTileIds), "An instance on a tile outside this Game's block is unreachable from the Game.");
        }
    }

    [Test, Description("Two Games from one map and one seed hold identical resources, so a seed reproduces a Game exactly.")]
    public async Task ShouldCreateIdenticalInstancesWhenSeedAndMapRepeatAsync()
    {
        await SeedForestMapWithPlacementsAsync();

        ServiceResultBase.ServiceResult<int> first = await _service.SetupGameAsync(BuildRequest("Seeded Game One", seed: 999), CancellationToken.None);
        ServiceResultBase.ServiceResult<int> second = await _service.SetupGameAsync(BuildRequest("Seeded Game Two", seed: 999), CancellationToken.None);

        Assert.That(first.HasSucceeded && second.HasSucceeded, Is.True, () => string.Join("; ", first.Errors.Concat(second.Errors)));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Dictionary<(int, byte), string> firstValues = await ReadRolledValuesAsync(verify, first.SuccessValue);
        Dictionary<(int, byte), string> secondValues = await ReadRolledValuesAsync(verify, second.SuccessValue);

        Assert.That(secondValues, Is.EqualTo(firstValues), "One seed and one map must roll the same resources, or a seed reproduces nothing.");
    }

    [Test, Description("Two Games from one map with different seeds hold different resources, so a map does not play out the same way twice.")]
    public async Task ShouldCreateDifferentInstancesWhenSeedDiffersAsync()
    {
        await SeedForestMapWithPlacementsAsync();

        ServiceResultBase.ServiceResult<int> first = await _service.SetupGameAsync(BuildRequest("Varied Game One", seed: 1000), CancellationToken.None);
        ServiceResultBase.ServiceResult<int> second = await _service.SetupGameAsync(BuildRequest("Varied Game Two", seed: 2000), CancellationToken.None);

        Assert.That(first.HasSucceeded && second.HasSucceeded, Is.True, () => string.Join("; ", first.Errors.Concat(second.Errors)));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Dictionary<(int, byte), string> firstValues = await ReadRolledValuesAsync(verify, first.SuccessValue);
        Dictionary<(int, byte), string> secondValues = await ReadRolledValuesAsync(verify, second.SuccessValue);

        Assert.That(secondValues, Is.Not.EqualTo(firstValues), "Two seeds producing one outcome would mean the seed decides nothing.");
    }

    [Test, Description("A Game whose MapResources cannot be created is failed rather than left holding none.")]
    public async Task ShouldFailSetupWhenInstantiationFailsAsync()
    {
        await SeedForestMapWithPlacementsAsync();

        var failing = Substitute.For<IMapResourceInstantiationService>();
        failing
            .InstantiateForGameAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.ServiceResult<int>.Fail("The MapResources could not be created."));

        GameSetupService service = BuildGameSetupService(failing);

        ServiceResultBase.ServiceResult<int> result = await service.SetupGameAsync(BuildRequest("Doomed Game", seed: 555), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "A Game that silently holds no resources is worse than one that was not created.");
            Assert.That(result.Errors, Is.Not.Empty, "The failure must say what went wrong.");
        }
    }
}
