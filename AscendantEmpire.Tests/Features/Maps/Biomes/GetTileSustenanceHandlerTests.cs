using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="GetTileSustenanceHandler"/>: that the tile toolbar is told what is available to the Player on a tile they can see, and nothing about a tile they cannot.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler's own reads are LINQ only; sight and geometry are substituted, since the sight rules are covered by their own tests and all this handler
/// does with them is ask whether the tile is in view. The Player's values come from the real <see cref="PlayerSustenanceValueReader"/>, so a modifier here is applied exactly as
/// in play.
/// </para>
/// <para>
/// The map is 10 x 10. The catalogue has one biome, Grassland (Forage 400, Hunt 100, no Fish, Fodder 1,000). By default the Player has one Force at (3, 4) seeing only its own
/// Grassland tile.
/// </para>
/// <para>
/// The support level comes from the real <see cref="TileSupportEvaluator"/>. Which Forces count, and the thresholds, are covered in <c>TileSupportEvaluatorTests</c> and
/// <c>TileSupportForecasterTests</c>; here it is only checked that the level reaches the response.
/// </para>
/// </remarks>
[TestFixture]
internal class GetTileSustenanceHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _SEEN_X = 3;
    private const int _SEEN_Y = 4;

    private ISightDataReader _sightDataReader = null!;
    private IFieldOfViewService _fieldOfViewService = null!;
    private GetTileSustenanceHandler _handler = null!;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1m,
                IsTraversable = true,
                MapEditorHtmlColor = "#7cb342",
                BaseForageValue = ScaledAmount.FromWhole(400).RawValue,
                BaseHuntValue = ScaledAmount.FromWhole(100).RawValue,
                BaseFishValue = null,
                BaseFodderValue = ScaledAmount.FromWhole(1_000).RawValue
            });

            await context.SaveChangesAsync();
        }

        _sightDataReader = Substitute.For<ISightDataReader>();
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 10, Height: 10, WrapsHorizontally: false, WrapsVertically: false));

        _fieldOfViewService = Substitute.For<IFieldOfViewService>();
        SetSeenTiles(BuildSeenTile(_SEEN_X, _SEEN_Y, BiomeType.Grassland));

        _handler = new GetTileSustenanceHandler(NullLogger<GetTileSustenanceHandler>.Instance, DbFactory, _sightDataReader, _fieldOfViewService, new PlayerSustenanceValueReader(), new TileSupportEvaluator(new PlayerSustenanceValueReader(), new BiomeRegenerationValueReader()));
    }

    [Test, Description("A tile in live sight reports the Player's values for its biome, which with no modifiers and nothing taken are the biome's base values.")]
    public async Task ShouldReturnTheBiomeValuesWhenTheTileIsObservedAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsObserved, Is.True);
            Assert.That(result.SuccessValue.Forage, Is.EqualTo(400m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(100m));
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A resource the biome does not have comes back null rather than 0, so the toolbar can leave it out instead of showing a stripped tile.")]
    public async Task ShouldReturnNullForAResourceTheBiomeDoesNotHaveAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Fish, Is.Null);
    }

    [Test, Description("The values are the calling Player's own, so a modifier they hold raises what the toolbar shows them.")]
    public async Task ShouldApplyThePlayersModifiersAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.PlayerSustenanceModifiers.Add(new PlayerSustenanceModifier
            {
                GameId = _GAME_ID,
                PlayerId = _PLAYER_ID,
                SourceType = ModifierSourceType.Technology,
                SourceId = 10,
                Resource = SustenanceResource.Hunt,
                BiomeTypeId = BiomeType.Grassland,
                FlatValue = ScaledAmount.FromWhole(100).RawValue,
                PercentValue = 0
            });

            await context.SaveChangesAsync();
        }

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Hunt, Is.EqualTo(200m));
    }

    [Test, Description("What has been taken from the tile is subtracted from each resource separately, so depletion of one resource never shows against another.")]
    public async Task ShouldSubtractEachResourcesOwnTakenAmountAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: ScaledAmount.FromWhole(150).RawValue, huntTaken: ScaledAmount.FromDecimal(25.5m).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Forage, Is.EqualTo(250m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(74.5m));
            Assert.That(result.SuccessValue.Fish, Is.Null, "A resource the biome does not have stays absent, even on a depleted tile.");
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A tile stripped past the Player's own value by a stronger Player shows 0 rather than a negative amount.")]
    public async Task ShouldShowZeroWhenMoreHasBeenTakenThanThePlayersValueAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: 0, huntTaken: ScaledAmount.FromWhole(180).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.Hunt, Is.EqualTo(0m));
    }

    [Test, Description("Depletion is read for the tile asked about only, so another tile having been fed on changes nothing here.")]
    public async Task ShouldIgnoreDepletionOnAnotherTileAsync()
    {
        await AddDepletionAsync(SeenTileId + 1, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: ScaledAmount.FromWhole(1_000).RawValue);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Forage, Is.EqualTo(400m));
            Assert.That(result.SuccessValue.Hunt, Is.EqualTo(100m));
            Assert.That(result.SuccessValue.Fodder, Is.EqualTo(1_000m));
        }
    }

    [Test, Description("A tile outside live sight discloses nothing, because what is left on a tile changes as anyone feeds and would leak where other Forces have been.")]
    public async Task ShouldDiscloseNothingWhenTheTileIsNotObservedAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X + 1, _SEEN_Y);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "An unwatched tile is an ordinary answer, not an error.");
            Assert.That(result.SuccessValue!.IsObserved, Is.False);
            Assert.That(result.SuccessValue.Forage, Is.Null);
            Assert.That(result.SuccessValue.Hunt, Is.Null);
            Assert.That(result.SuccessValue.Fish, Is.Null);
            Assert.That(result.SuccessValue.Fodder, Is.Null);
        }
    }

    [Test, Description("The toolbar is told whether the tile can feed the Player's Forces on it: 300 troops against 500 Forage and Hunt is one Turn.")]
    public async Task ShouldReturnTheSupportLevelForTheForcesOnTheTileAsync()
    {
        await AddForceAsync(troops: 300);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.EqualTo(TileSupportLevel.OneTurn));
    }

    [Test, Description("A tile with food and no Forces on it has no support level, so the toolbar is not coloured.")]
    public async Task ShouldReturnNoSupportLevelWhenNoForcesAreOnTheTileAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.Null);
    }

    [Test, Description("A stripped tile is exhausted even with no Forces on it, so the toolbar shows a bare tile as bare.")]
    public async Task ShouldReturnExhaustedForAStrippedTileWithNoForcesAsync()
    {
        await AddDepletionAsync(SeenTileId, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.EqualTo(TileSupportLevel.Exhausted));
    }

    [Test, Description("A tile outside live sight has no support level either, since a level would disclose what is left on it.")]
    public async Task ShouldReturnNoSupportLevelWhenTheTileIsNotObservedAsync()
    {
        await AddDepletionAsync(SeenTileId + 1, forageTaken: ScaledAmount.FromWhole(400).RawValue, huntTaken: ScaledAmount.FromWhole(100).RawValue, fodderTaken: 0);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X + 1, _SEEN_Y);

        Assert.That(result.SuccessValue!.SupportLevel, Is.Null);
    }

    [Test, Description("A coordinate past the edge of the map names no tile and is a 404, rather than being wrapped onto a tile that is on the map.")]
    public async Task ShouldReturnNotFoundWhenTheTileIsOffTheMapAsync()
    {
        ServiceResult<TileSustenance> result = await HandleAsync(10, _SEEN_Y);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A Game with no map geometry does not exist, so the read is a 404 rather than an empty answer.")]
    public async Task ShouldReturnNotFoundWhenTheGameDoesNotExistAsync()
    {
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns((CampaignMapGeometry?)null);

        ServiceResult<TileSustenance> result = await HandleAsync(_SEEN_X, _SEEN_Y);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    /// <summary>
    /// The seen tile's id, as <see cref="BuildSeenTile"/> numbers tiles on the 10 x 10 map.
    /// </summary>
    private static int SeenTileId => 1 + (_SEEN_Y * 10) + _SEEN_X;

    /// <summary>
    /// Records raw scaled amounts as already taken from a tile.
    /// </summary>
    private async Task AddDepletionAsync(int tileId, long forageTaken, long huntTaken, long fodderTaken)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = forageTaken,
            HuntTaken = huntTaken,
            FodderTaken = fodderTaken,
            FedOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Adds a healthy Force of the Player's on the seen tile.
    /// </summary>
    private async Task AddForceAsync(int troops)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = 1,
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = SeenTileId,
            UnitTemplateId = 1,
            InitialStrength = troops,
            HealthyCount = troops,
            InjuredCount = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task<ServiceResult<TileSustenance>> HandleAsync(int x, int y)
    {
        return await _handler.Handle(new GetTileSustenanceRequest(_GAME_ID, _PLAYER_ID, x, y), CancellationToken.None);
    }

    /// <summary>
    /// Has the field-of-view service report one Force of the Player's seeing exactly the given tiles.
    /// </summary>
    private void SetSeenTiles(params SightTerrain[] seenTiles)
    {
        var observer = new SightObserver
        {
            Id = 1,
            RecordType = RecordType.Unit,
            PlayerId = _PLAYER_ID,
            Location = new Coord(_SEEN_X, _SEEN_Y),
            BaseSightRange = 0,
            Detection = 0
        };

        IReadOnlyCollection<ObserverFieldOfView> fieldsOfView = [new ObserverFieldOfView(observer, seenTiles)];

        _fieldOfViewService
            .GetPlayerFieldOfViewAsync(_GAME_ID, _PLAYER_ID, Arg.Any<CampaignMapGeometry>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Succeed(fieldsOfView));
    }

    private static SightTerrain BuildSeenTile(int x, int y, BiomeType biome)
    {
        return new SightTerrain
        {
            CampaignMapId = 1 + (y * 10) + x,
            X = x,
            Y = y,
            BiomeType = biome,
            GeographyType = CampaignMapGeographyType.Flat,
            DoesBlockSight = false,
            RelativeElevation = 0,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            BiomeName = biome.ToString(),
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            BiomeIsTraversable = true
        };
    }
}
