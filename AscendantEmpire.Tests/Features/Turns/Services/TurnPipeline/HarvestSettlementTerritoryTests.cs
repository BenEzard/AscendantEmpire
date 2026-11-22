using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="HarvestSettlementTerritory"/>: which tiles a Settlement draws from at the start of a Round, how much it takes, and what it banks.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process reads and writes through LINQ and the change tracker only, and the credit service is substituted so the tests assert on
/// the credits it is handed rather than on bank rows.
/// </para>
/// <para>
/// How much one tile yields is pinned without a database in <c>SettlementHarvestCalculatorTests</c>. What is tested here is the orchestration: the territory,
/// the depletion writes and the banking.
/// </para>
/// <para>
/// The catalogue is the design doc's worked example. Grassland is Forage 40, Hunt 70, Fodder 20, Regeneration Percent 25%, so a tile regrows 10 Forage, 17.5 Hunt
/// and 5 Fodder a Round; Coastal is Fish 100 at 25%, so 25; and a legacy Desert has Forage 100 and no Regeneration Percent at all. The village has a population
/// of 100, which is more than enough to work every tile.
/// </para>
/// </remarks>
[TestFixture]
internal class HarvestSettlementTerritoryTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;
    private const int _PLAYER_ID = 7;
    private const int _OTHER_PLAYER_ID = 8;
    private const int _VILLAGE_ID = 11;
    private const int _CAMP_ID = 12;
    private const int _RAZED_VILLAGE_ID = 13;
    private const int _OTHER_GAME_VILLAGE_ID = 14;
    private const int _VILLAGE_POPULATION = 100;
    private const int _CURRENT_ROUND_NUMBER = 4;

    private const int _VILLAGE_TILE_ID = 101;
    private const int _VILLAGE_SECOND_TILE_ID = 102;
    private const int _VILLAGE_WATER_TILE_ID = 103;
    private const int _VILLAGE_DESERT_TILE_ID = 104;
    private const int _UNOWNED_TILE_ID = 105;
    private const int _CAMP_TILE_ID = 106;
    private const int _RAZED_VILLAGE_TILE_ID = 107;
    private const int _OTHER_GAME_TILE_ID = 108;

    /// <summary>
    /// A one-row map whose world ids are the tile ids: <c>X + 101</c>, matching the tiles seeded below.
    /// </summary>
    private static readonly CampaignMapGeometry _GEOMETRY = new(CampaignMapOffsetId: 101, Width: 10, Height: 1, WrapsHorizontally: false, WrapsVertically: false);

    private readonly List<AssetCredit> _creditsHandedOver = [];

    private Dictionary<string, int> _assetIdsByCode = [];

    private IAssetBankCreditService _creditService = null!;

    private HarvestSettlementTerritory _process = null!;

    [SetUp]
    public async Task SetUpProcessAsync()
    {
        _creditsHandedOver.Clear();

        await SeedWorldAsync();

        _creditService = Substitute.For<IAssetBankCreditService>();
        StubCreditService(credit => ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed(Placements(credit, rawUnplaced: 0)));

        // Substituted, because the Game row the real reader would need is not seeded here. The geometry puts this Game's tiles at X 0-7 of a one-row map whose
        // world ids start at 101, so the river reader finds the rows the tests seed.
        var sightDataReader = Substitute.For<ISightDataReader>();
        sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_GEOMETRY);

        _process = new HarvestSettlementTerritory(
            DbFactory,
            new PlayerSustenanceValueReader(),
            new BiomeRegenerationValueReader(),
            new SustenanceAssetReader(),
            new MaterialAssetReader(),
            sightDataReader,
            new CampaignRiverReader(),
            _creditService,
            NullLogger<HarvestSettlementTerritory>.Instance);
    }

    #region Sustenance

    [Test, Description("Pins that a village harvests the regrowth of every tile of its territory, which is what it lives on.")]
    public async Task ShouldCreditTheRegrowthOfEveryTerritoryTileWhenTheVillageIsStaffedAsync()
    {
        string error = await _process.ProcessAsync(StartRoundContext());

        // Two grassland tiles at 10 Forage each, and the Hunt and Fodder beside it.
        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Empty);
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(20)));
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(35)));
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Fodder), Is.EqualTo(ScaledAmount.FromWhole(10)));
        });
    }

    [Test, Description("Pins that what is harvested is added to the tile's shared Taken, so the land a settlement works is the same land a Force eats from.")]
    public async Task ShouldAddTheHarvestToTheTilesTakenWhenItHarvestsAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_VILLAGE_TILE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(ScaledAmount.FromWhole(10).RawValue));
            Assert.That(depletion.HuntTaken, Is.EqualTo(ScaledAmount.FromRaw(17_500).RawValue));
        });
    }

    [Test, Description("Pins that a harvest does not mark the tile as fed on, since that marker records a Force being present.")]
    public async Task ShouldLeaveFedOnRoundAloneWhenItHarvestsAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_VILLAGE_TILE_ID);

        Assert.That(depletion.FedOnRound, Is.Null);
    }

    [Test, Description("Pins that a tile with no depletion row is given one, so a settlement's first harvest is recorded like any other take.")]
    public async Task ShouldCreateADepletionRowWhenTheTileHasNeverBeenTakenFromAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Assert.That(await context.CampaignMapDepletions.AnyAsync(), Is.False, "The world is seeded with no depletion rows at all.");
        }

        await _process.ProcessAsync(StartRoundContext());

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            Assert.That(await context.CampaignMapDepletions.AnyAsync(row => row.CampaignMapId == _VILLAGE_TILE_ID), Is.True);
        }
    }

    [Test, Description("Pins that a tile already drawn down yields only what is left, so a settlement cannot overdraw ground a Force has stripped.")]
    public async Task ShouldTakeOnlyWhatIsLeftWhenTheTileIsAlreadyDrawnDownAsync()
    {
        // 35 of the tile's 40 Forage is gone, so 5 is left where the regrowth would otherwise allow 10.
        await AddDepletionAsync(_VILLAGE_TILE_ID, forageTaken: ScaledAmount.FromWhole(35).RawValue);

        await _process.ProcessAsync(StartRoundContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_VILLAGE_TILE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(depletion.ForageTaken, Is.EqualTo(ScaledAmount.FromWhole(40).RawValue), "The tile is stripped and no further.");

            // 5 off the drawn-down tile, and the other tile's full 10.
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(15)));
        });
    }

    [Test, Description("Pins that only the settlement's own territory is harvested, so a neighbour's ground is never drawn on.")]
    public async Task ShouldHarvestNoTileWhenItIsNotInTheSettlementsTerritoryAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await context.CampaignMapDepletions.AnyAsync(row => row.CampaignMapId == _UNOWNED_TILE_ID), Is.False);
    }

    [Test, Description("Pins that a camp harvests the single tile it stands on, which is the whole of a radius-0 territory.")]
    public async Task ShouldHarvestOnlyItsOwnTileWhenTheSettlementIsACampAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        Assert.Multiple(() =>
        {
            Assert.That(CreditedTo(_CAMP_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(10)), "One tile's worth, not the village's two.");
            Assert.That(_creditsHandedOver.Where(credit => credit.SettlementId == _CAMP_ID).Select(credit => credit.PlayerId), Is.All.EqualTo(_OTHER_PLAYER_ID));
        });
    }

    [Test, Description("Pins that settlements in other Games are untouched, since the start-of-Round pipeline is game-scoped rather than player-scoped.")]
    public async Task ShouldHarvestNothingWhenTheSettlementBelongsToAnotherGameAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        Assert.That(_creditsHandedOver.Any(credit => credit.SettlementId == _OTHER_GAME_VILLAGE_ID), Is.False);
    }

    [Test, Description("Pins that an owned water tile is harvested for its Fish, so no rule is needed to tell a coastal territory from a landlocked one.")]
    public async Task ShouldCreditTheFishWhenTheTerritoryTileIsWaterAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Fish), Is.EqualTo(ScaledAmount.FromWhole(25)));
    }

    [Test, Description("Pins that a razed settlement harvests nothing, its territory having been abandoned with it.")]
    public async Task ShouldHarvestNothingWhenTheSettlementsStructureIsRemovedAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        Assert.That(_creditsHandedOver.Any(credit => credit.SettlementId == _RAZED_VILLAGE_ID), Is.False);
    }

    #endregion

    #region Bad data and limits

    [Test, Description("Pins that a biome with no Regeneration Percent is skipped, because without a regrowth there is no cap to harvest under.")]
    public async Task ShouldHarvestNoSustenanceWhenTheBiomeHasNoRegenerationPercentAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        CampaignMapDepletion? depletion = await context.CampaignMapDepletions.FirstOrDefaultAsync(row => row.CampaignMapId == _VILLAGE_DESERT_TILE_ID);

        Assert.That(depletion?.ForageTaken ?? 0, Is.Zero);
    }

    [Test, Description("Pins that one settlement whose credit fails does not stop the others harvesting, so a missing bank cannot block a Round.")]
    public async Task ShouldHarvestTheOtherSettlementsWhenOneSettlementsCreditFailsAsync()
    {
        StubCreditService(credit => credit.SettlementId == _VILLAGE_ID
            ? ServiceResult<IReadOnlyList<AssetCreditPlacement>>.NotFound("That settlement has no bank.")
            : ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed(Placements(credit, rawUnplaced: 0)));

        string error = await _process.ProcessAsync(StartRoundContext());

        CampaignMapDepletion? villageDepletion;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            villageDepletion = await context.CampaignMapDepletions.FirstOrDefaultAsync(row => row.CampaignMapId == _VILLAGE_TILE_ID);
        }

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Empty, "A settlement that cannot bank its harvest is not a reason to refuse the Round.");
            Assert.That(CreditedTo(_CAMP_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(10)), "The camp still harvests.");
            Assert.That(villageDepletion?.ForageTaken ?? 0, Is.Zero, "A settlement that could not bank its harvest did not take it off the tile either.");
        });
    }

    [Test, Description("Pins that a full network is a success carrying an unplaced figure, since an overflowing store must not refuse a Round.")]
    public async Task ShouldSucceedWhenTheNetworkIsFullAndNothingCouldBePlacedAsync()
    {
        StubCreditService(credit => ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed(Placements(credit, rawUnplaced: credit.Amount.RawValue)));

        string error = await _process.ProcessAsync(StartRoundContext());

        CampaignMapDepletion depletion = await ReloadDepletionAsync(_VILLAGE_TILE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Empty);
            Assert.That(depletion.ForageTaken, Is.EqualTo(ScaledAmount.FromWhole(10).RawValue), "The tile is still drawn down: the food was taken and then wasted.");
        });
    }

    #endregion

    #region Materials

    [Test, Description("Pins that a territory tile's materials are banked at the biome's base value, which is what a static resource yields every Round.")]
    public async Task ShouldCreditTheBiomesMaterialBaseValuesWhenTheTileHasThemAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        // Two grassland tiles at 40 each; the coastal and desert tiles hold neither.
        Assert.Multiple(() =>
        {
            Assert.That(CreditedTo(_VILLAGE_ID, MaterialResource.Wood), Is.EqualTo(ScaledAmount.FromWhole(80)));
            Assert.That(CreditedTo(_VILLAGE_ID, MaterialResource.Stone), Is.EqualTo(ScaledAmount.FromWhole(80)));
        });
    }

    [Test, Description("Pins that a tile stripped of its food still yields its materials in full, since nothing a Settlement takes depletes a material value.")]
    public async Task ShouldCreditTheMaterialsInFullWhenTheTilesSustenanceIsStrippedAsync()
    {
        await AddDepletionAsync(_VILLAGE_TILE_ID, forageTaken: ScaledAmount.FromWhole(40).RawValue);

        await _process.ProcessAsync(StartRoundContext());

        Assert.Multiple(() =>
        {
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(10)), "Only the tile that still has Forage gives any.");
            Assert.That(CreditedTo(_VILLAGE_ID, MaterialResource.Wood), Is.EqualTo(ScaledAmount.FromWhole(80)), "Both tiles still give their whole Wood.");
        });
    }

    [Test, Description("Pins that a material the biome does not have is not credited, so an absent seam banks nothing rather than zero.")]
    public async Task ShouldCreditNothingWhenTheBiomeHasNoValueForTheMaterialAsync()
    {
        await _process.ProcessAsync(StartRoundContext());

        Assert.That(CreditedTo(_VILLAGE_ID, MaterialResource.Bone), Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Pins that a material whose Asset is missing from the catalogue is skipped and the rest of the harvest still lands.")]
    public async Task ShouldHarvestTheOtherMaterialsWhenOneMaterialsAssetIsNotSeededAsync()
    {
        await RemoveAssetAsync(MaterialResourceAssets.CodesByResource[MaterialResource.Stone]);

        string error = await _process.ProcessAsync(StartRoundContext());

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Empty);
            Assert.That(CreditedTo(_VILLAGE_ID, MaterialResource.Wood), Is.EqualTo(ScaledAmount.FromWhole(80)));
            Assert.That(CreditedTo(_VILLAGE_ID, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(20)));
        });
    }

    #endregion

    #region Helpers

    private static TurnPipelineContextData StartRoundContext() =>
        new(TurnPipelineType.StartCampaignRound, _GAME_ID, _CURRENT_ROUND_NUMBER);

    /// <summary>
    /// What the process asked to be credited to one Settlement for one sustenance resource, summed across its territory.
    /// </summary>
    private ScaledAmount CreditedTo(int settlementId, SustenanceResource resource)
    {
        int assetId = _assetIdsByCode[SustenanceResourceAssets.CodesByResource[resource]];

        return _creditsHandedOver
            .Where(credit => credit.SettlementId == settlementId && credit.AssetId == assetId)
            .Aggregate(ScaledAmount.Zero, (total, credit) => total + credit.Amount);
    }

    /// <summary>
    /// What the process asked to be credited to one Settlement for one material resource, summed across its territory.
    /// </summary>
    private ScaledAmount CreditedTo(int settlementId, MaterialResource resource)
    {
        int assetId = _assetIdsByCode[MaterialResourceAssets.CodesByResource[resource]];

        return _creditsHandedOver
            .Where(credit => credit.SettlementId == settlementId && credit.AssetId == assetId)
            .Aggregate(ScaledAmount.Zero, (total, credit) => total + credit.Amount);
    }

    /// <summary>
    /// Makes the substituted credit service answer per credit, and records every credit it was handed.
    /// </summary>
    private void StubCreditService(Func<AssetCredit, ServiceResult<IReadOnlyList<AssetCreditPlacement>>> answer)
    {
        _creditService
            .CreditAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<IReadOnlyList<AssetCredit>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                IReadOnlyList<AssetCredit> credits = call.ArgAt<IReadOnlyList<AssetCredit>>(1);
                _creditsHandedOver.AddRange(credits);

                // The first credit's verdict stands for the call, which is enough: the process calls once per Settlement.
                return credits.Count == 0
                    ? ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed([])
                    : answer(credits[0]);
            });
    }

    private static IReadOnlyList<AssetCreditPlacement> Placements(AssetCredit credit, long rawUnplaced) =>
        [new AssetCreditPlacement(credit, StorageClassId: 1, RawPlaced: credit.Amount.RawValue - rawUnplaced, RawUnplaced: rawUnplaced)];

    /// <summary>
    /// Takes one Asset back out of the catalogue, standing in for a resource nothing has seeded an Asset for.
    /// </summary>
    private async Task RemoveAssetAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Asset asset = await context.Assets.FirstAsync(row => row.Code == code);

        context.Assets.Remove(asset);

        await context.SaveChangesAsync();
    }

    private async Task<CampaignMapDepletion> ReloadDepletionAsync(int tileId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapDepletions.FirstAsync(row => row.CampaignMapId == tileId);
    }

    private async Task AddDepletionAsync(int tileId, long forageTaken)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = forageTaken
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Writes the biome catalogue, the Assets the harvest banks into, the tiles, and the four Settlements the tests draw on.
    /// </summary>
    private async Task SeedWorldAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        var grassland = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342",
            RegenerationPercent = 0.25m,
            BaseForageValue = ScaledAmount.FromWhole(40).RawValue,
            BaseHuntValue = ScaledAmount.FromWhole(70).RawValue,
            BaseFishValue = null,
            BaseFodderValue = ScaledAmount.FromWhole(20).RawValue,
            BaseWoodValue = ScaledAmount.FromWhole(40).RawValue,
            BaseStoneValue = ScaledAmount.FromWhole(40).RawValue,
            BaseBoneValue = null
        };

        var coastal = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Coastal,
            Name = "Coastal",
            MovementCost = 1m,
            IsWater = true,
            IsTraversable = true,
            MapEditorHtmlColor = "#1565c0",
            RegenerationPercent = 0.25m,
            BaseForageValue = null,
            BaseHuntValue = null,
            BaseFishValue = ScaledAmount.FromWhole(100).RawValue,
            BaseFodderValue = null
        };

        // Saved before the biome validator required a Regeneration Percent wherever there is a sustenance base.
        var legacyDesert = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Desert,
            Name = "Desert",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorHtmlColor = "#e0c068",
            RegenerationPercent = null,
            BaseForageValue = ScaledAmount.FromWhole(100).RawValue
        };

        context.CampaignGeographies.Add(flat);
        context.CampaignBiomes.AddRange(grassland, coastal, legacyDesert);

        context.CampaignMaps.AddRange(
            BuildTile(_VILLAGE_TILE_ID, _GAME_ID, x: 0, grassland, flat, _PLAYER_ID, _VILLAGE_ID),
            BuildTile(_VILLAGE_SECOND_TILE_ID, _GAME_ID, x: 1, grassland, flat, _PLAYER_ID, _VILLAGE_ID),
            BuildTile(_VILLAGE_WATER_TILE_ID, _GAME_ID, x: 2, coastal, flat, _PLAYER_ID, _VILLAGE_ID),
            BuildTile(_VILLAGE_DESERT_TILE_ID, _GAME_ID, x: 3, legacyDesert, flat, _PLAYER_ID, _VILLAGE_ID),
            BuildTile(_UNOWNED_TILE_ID, _GAME_ID, x: 4, grassland, flat, ownerPlayerId: null, ownerSettlementId: null),
            BuildTile(_CAMP_TILE_ID, _GAME_ID, x: 5, grassland, flat, _OTHER_PLAYER_ID, _CAMP_ID),
            BuildTile(_RAZED_VILLAGE_TILE_ID, _GAME_ID, x: 6, grassland, flat, _PLAYER_ID, _RAZED_VILLAGE_ID),
            BuildTile(_OTHER_GAME_TILE_ID, _OTHER_GAME_ID, x: 7, grassland, flat, _PLAYER_ID, _OTHER_GAME_VILLAGE_ID));

        context.StructureInstances.AddRange(
            BuildStructure(id: 101, _GAME_ID, _VILLAGE_TILE_ID, _PLAYER_ID, removedOnTurnId: null),
            BuildStructure(id: 102, _GAME_ID, _CAMP_TILE_ID, _OTHER_PLAYER_ID, removedOnTurnId: null),
            BuildStructure(id: 103, _GAME_ID, _RAZED_VILLAGE_TILE_ID, _PLAYER_ID, removedOnTurnId: 9),
            BuildStructure(id: 104, _OTHER_GAME_ID, _OTHER_GAME_TILE_ID, _PLAYER_ID, removedOnTurnId: null));

        context.Settlements.AddRange(
            BuildSettlement(_VILLAGE_ID, _GAME_ID, structureInstanceId: 101, _PLAYER_ID, SettlementTier.Village, _VILLAGE_POPULATION),
            BuildSettlement(_CAMP_ID, _GAME_ID, structureInstanceId: 102, _OTHER_PLAYER_ID, SettlementTier.Camp, population: 50),
            BuildSettlement(_RAZED_VILLAGE_ID, _GAME_ID, structureInstanceId: 103, _PLAYER_ID, SettlementTier.Village, _VILLAGE_POPULATION),
            BuildSettlement(_OTHER_GAME_VILLAGE_ID, _OTHER_GAME_ID, structureInstanceId: 104, _PLAYER_ID, SettlementTier.Village, _VILLAGE_POPULATION));

        context.Assets.AddRange(BuildHarvestableAssets());

        await context.SaveChangesAsync();

        _assetIdsByCode = await context.Assets.ToDictionaryAsync(asset => asset.Code, asset => asset.Id);
    }

    /// <summary>
    /// An Asset for every sustenance and material resource, so nothing the harvest takes is left unbanked for want of a catalogue row.
    /// </summary>
    private static IEnumerable<Asset> BuildHarvestableAssets()
    {
        string[] codes =
        [
            .. SustenanceResourceAssets.CodesByResource.Values,
            .. MaterialResourceAssets.CodesByResource.Values
        ];

        return codes.Select(code => new Asset
        {
            Code = code,
            DisplayName = code,
            IconPath = "/resources/other/placeholder.svg",
            IsGradedAsset = false,
            SortOrder = 0,
            StorageClassId = 1
        });
    }

    private static CampaignMap BuildTile(
        int tileId,
        int gameId,
        int x,
        CampaignBiome biome,
        CampaignGeography geography,
        int? ownerPlayerId,
        int? ownerSettlementId)
    {
        return new CampaignMap
        {
            CampaignMapId = tileId,
            GameId = gameId,
            XCoord = x,
            YCoord = 0,
            BiomeTypeId = biome.BiomeTypeId,
            CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
            OwnedByPlayerId = ownerPlayerId,
            OwnedBySettlementId = ownerSettlementId,
            Biome = biome,
            Geography = geography,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        };
    }

    private static StructureInstance BuildStructure(int id, int gameId, int campaignMapTileId, int ownedByPlayerId, int? removedOnTurnId)
    {
        return new StructureInstance
        {
            Id = id,
            GameId = gameId,
            CampaignMapTileId = campaignMapTileId,
            StructureId = 1,
            SlotType = TileSlotType.Primary,
            OwnedByPlayerId = ownedByPlayerId,
            CreatedOnTurnId = 1,
            CompletedOnTurnId = 2,
            RemovedOnTurnId = removedOnTurnId
        };
    }

    private static Settlement BuildSettlement(int id, int gameId, int structureInstanceId, int foundingPlayerId, SettlementTier tier, int population)
    {
        return new Settlement
        {
            Id = id,
            GameId = gameId,
            StructureInstanceId = structureInstanceId,
            FoundingPlayerId = foundingPlayerId,
            Tier = tier,
            Population = population,
            FoundedOnRoundNumber = 1,
            FoundedOnTurnId = 1
        };
    }

    #endregion
}
