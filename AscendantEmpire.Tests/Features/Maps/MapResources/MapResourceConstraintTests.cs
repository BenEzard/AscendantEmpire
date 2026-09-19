using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Modifiers;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests that the database enforces the MapResource rules no code writes yet: at most two MapResources on a tile and one of each type, one habitat and one modifier per
/// source for each Biome and Geography (NULLs included), and a switch always naming exactly what its kind switches.
/// </summary>
/// <remarks>
/// <para>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is
/// restored from the migrated template, so this also proves the CM-47 migration created them.
/// </para>
/// <para>
/// The unique indexes over nullable columns rely on <c>HasFilter(null)</c>, the same as <c>PlayerSustenanceModifierConstraintTests</c>'; without it EF's SQL Server
/// convention would filter the NULL rows out of the index and let them duplicate.
/// </para>
/// </remarks>
[TestFixture]
internal class MapResourceConstraintTests : SqlServerTestBase
{
    private const int _SOURCE_ID = 10;

    private int _gameId;
    private int _playerId;
    private int _tileId;
    private MapResourceTestCatalogue _catalogue = null!;

    [SetUp]
    public async Task SeedGameAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _catalogue = await MapResourceTestSeed.SeedCatalogueAsync(context);
        await MapResourceTestSeed.EnsureGeographyAsync(context, CampaignMapGeographyType.Flat);

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = "MapResource Game",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var player = new Player
        {
            GameId = game.Id,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Ruler",
            FactionName = "Faction",
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        };

        context.Players.Add(player);

        // One tile is all these tests need: every rule under test is about what one tile, or one Player, can hold.
        var tile = new CampaignMap
        {
            CampaignMapId = 1,
            GameId = game.Id,
            XCoord = 0,
            YCoord = 0,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Biome = await context.CampaignBiomes.SingleAsync(b => b.BiomeTypeId == BiomeType.Forest),
            Geography = await context.CampaignGeographies.SingleAsync(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat),
            Game = game,
            FirstVisitor = null,
            LastVisitor = null,
            ControlledBy = null
        };

        context.CampaignMaps.Add(tile);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
        _tileId = tile.CampaignMapId;
    }

    #region Instances on a tile
    [Test, Description("A tile holds two different MapResources, one in each slot.")]
    public async Task ShouldAllowTwoDifferentMapResourcesOnATileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MapResourceInstances.AddRange(
            BuildInstance(_catalogue.BerryTypeId, slot: 1),
            BuildInstance(_catalogue.WoodTypeId, slot: 2));

        await context.SaveChangesAsync();

        Assert.That(await context.MapResourceInstances.CountAsync(i => i.CampaignMapId == _tileId), Is.EqualTo(2));
    }

    [Test, Description("There are only two slots, so a third MapResource cannot be placed on a tile.")]
    public async Task ShouldRejectASlotOutsideOneAndTwoAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MapResourceInstances.Add(BuildInstance(_catalogue.BerryTypeId, slot: 3));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("Two MapResources cannot share a slot, which with the slot check caps a tile at two.")]
    public async Task ShouldRejectTwoMapResourcesInTheSameSlotAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MapResourceInstances.Add(BuildInstance(_catalogue.BerryTypeId, slot: 1));
        await context.SaveChangesAsync();

        context.MapResourceInstances.Add(BuildInstance(_catalogue.WoodTypeId, slot: 1));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A tile holds at most one of each type, since spreading into a tile with the same type tops it up rather than adding another.")]
    public async Task ShouldRejectTheSameTypeTwiceOnATileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MapResourceInstances.Add(BuildInstance(_catalogue.BerryTypeId, slot: 1));
        await context.SaveChangesAsync();

        context.MapResourceInstances.Add(BuildInstance(_catalogue.BerryTypeId, slot: 2));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("Extraction can never take more than is on the tile, so a negative quantity could only come from a defect.")]
    public async Task ShouldRejectANegativeQuantityAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceInstance overdrawn = BuildInstance(_catalogue.BerryTypeId, slot: 1);
        overdrawn.Quantity = -1;

        context.MapResourceInstances.Add(overdrawn);

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }
    #endregion

    #region Catalogue
    [Test, Description("A type has one Biome-wide habitat per Biome, because NULL Geographies must collide in the unique index rather than be filtered out of it.")]
    public async Task ShouldRejectASecondBiomeWideHabitatAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MapResourceHabitats.Add(new MapResourceHabitat
        {
            MapResourceTypeId = _catalogue.BerryTypeId,
            BiomeTypeId = BiomeType.Forest,
            CampaignMapGeographyTypeId = null,
            InitialQuantityMin = 1_000,
            InitialQuantityMax = 2_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.1m
        });

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A type cannot be both renewable and stratified, even if written without the validator.")]
    public async Task ShouldRejectATypeThatIsBothRenewableAndStratifiedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceType coal = await context.MapResourceTypes.SingleAsync(t => t.Id == _catalogue.CoalTypeId);
        coal.IsRenewable = true;

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }
    #endregion

    #region Per-Player rows
    [Test, Description("A source cannot hold two every-Biome, every-Geography modifiers for the same type and target.")]
    public async Task ShouldRejectASecondAllBiomesAllGeographiesModifierFromTheSameSourceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerMapResourceModifiers.Add(BuildModifier(flatValue: 1_000));
        await context.SaveChangesAsync();

        context.PlayerMapResourceModifiers.Add(BuildModifier(flatValue: 2_000));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("A modifier with neither a flat nor a percentage part changes nothing, so it is rejected.")]
    public async Task ShouldRejectAModifierWithNoValueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerMapResourceModifiers.Add(BuildModifier(flatValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [TestCase(MapResourceSwitchKind.Discovery, false, false, Description = "A discovery switch names neither a Resource nor a layer.")]
    [TestCase(MapResourceSwitchKind.YieldResource, true, false, Description = "A yield switch names the Resource it switches.")]
    [TestCase(MapResourceSwitchKind.SurveyLayer, false, true, Description = "A survey switch names the layer it switches.")]
    public async Task ShouldAllowASwitchNamingExactlyWhatItsKindSwitchesAsync(MapResourceSwitchKind kind, bool namesResource, bool namesLayer)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerMapResourceSwitches.Add(BuildSwitch(kind, namesResource, namesLayer));

        await context.SaveChangesAsync();

        Assert.That(await context.PlayerMapResourceSwitches.CountAsync(s => s.PlayerId == _playerId), Is.EqualTo(1));
    }

    [TestCase(MapResourceSwitchKind.Discovery, false, true, Description = "A discovery switch cannot name a layer.")]
    [TestCase(MapResourceSwitchKind.YieldResource, false, false, Description = "A yield switch must name its Resource.")]
    [TestCase(MapResourceSwitchKind.SurveyLayer, true, true, Description = "A survey switch cannot also name a Resource.")]
    public async Task ShouldRejectASwitchThatDoesNotNameExactlyWhatItsKindSwitchesAsync(MapResourceSwitchKind kind, bool namesResource, bool namesLayer)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerMapResourceSwitches.Add(BuildSwitch(kind, namesResource, namesLayer));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }
    #endregion

    /// <summary>
    /// An active instance of the type on the fixture's tile.
    /// </summary>
    private MapResourceInstance BuildInstance(int typeId, byte slot)
    {
        return new MapResourceInstance
        {
            GameId = _gameId,
            CampaignMapId = _tileId,
            MapResourceTypeId = typeId,
            Slot = slot,
            Quantity = typeId == _catalogue.BerryTypeId ? 300_000 : null,
            EstimationFuzzyFactor = 0.2m,
            MapResourceDistributionScenarioId = typeId == _catalogue.WoodTypeId ? _catalogue.ScenarioId : null,
            CreatedOnRound = 0
        };
    }

    /// <summary>
    /// A MaximumQuantity modifier to Berry, for every Biome and Geography, from the fixture's one Technology source.
    /// </summary>
    private PlayerMapResourceModifier BuildModifier(long flatValue)
    {
        return new PlayerMapResourceModifier
        {
            GameId = _gameId,
            PlayerId = _playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = _SOURCE_ID,
            MapResourceTypeId = _catalogue.BerryTypeId,
            Target = MapResourceModifierTarget.MaximumQuantity,
            BiomeTypeId = null,
            CampaignMapGeographyTypeId = null,
            FlatValue = flatValue,
            PercentValue = 0
        };
    }

    private PlayerMapResourceSwitch BuildSwitch(MapResourceSwitchKind kind, bool namesResource, bool namesLayer)
    {
        return new PlayerMapResourceSwitch
        {
            GameId = _gameId,
            PlayerId = _playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = _SOURCE_ID,
            Kind = kind,
            MapResourceTypeId = _catalogue.BerryTypeId,
            ResourceId = namesResource ? _catalogue.ResourceId : null,
            LayerNumber = namesLayer ? 2 : null,
            IsEnabled = true
        };
    }
}
