using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Handlers;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="GetTileSupportLevelsHandler"/>: that the hover marker is told the support level of every tile in live sight for the selected Forces, and nothing
/// about any other tile.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. Sight and geometry are substituted, as in <c>GetTileSustenanceHandlerTests</c>; the evaluator and the Player's values are real. Which Forces make up
/// a need, and the forecast, are covered in <c>TileSupportEvaluatorTests</c> and <c>TileSupportForecasterTests</c>.
/// </para>
/// <para>
/// The map is 10 x 10. Grassland has Forage 400 and Hunt 100: 500 food, with no Regeneration Percent unless a test sets one. The Player's one Force, of 300 troops, stands at (3, 4) and sees (3, 4) and (4, 4). (5, 4) is out
/// of sight.
/// </para>
/// </remarks>
[TestFixture]
internal class GetTileSupportLevelsHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;
    private const int _FORCE_ID = 1;
    private const int _OTHER_PLAYERS_FORCE_ID = 2;
    private const int _FORCE_X = 3;
    private const int _Y = 4;

    private ISightDataReader _sightDataReader = null!;
    private IFieldOfViewService _fieldOfViewService = null!;
    private GetTileSupportLevelsHandler _handler = null!;

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
                BaseFodderValue = null
            });

            context.UnitInstances.AddRange(
                BuildForce(_FORCE_ID, _PLAYER_ID, TileId(_FORCE_X, _Y), troops: 300),
                BuildForce(_OTHER_PLAYERS_FORCE_ID, _OTHER_PLAYER_ID, TileId(8, 8), troops: 50));

            await context.SaveChangesAsync();
        }

        _sightDataReader = Substitute.For<ISightDataReader>();
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 10, Height: 10, WrapsHorizontally: false, WrapsVertically: false));

        _fieldOfViewService = Substitute.For<IFieldOfViewService>();

        // Two Observers seeing the Force's own tile, so the handler has to cope with a tile seen twice.
        SetFieldsOfView(
            [BuildSeenTile(_FORCE_X, _Y), BuildSeenTile(_FORCE_X + 1, _Y)],
            [BuildSeenTile(_FORCE_X, _Y)]);

        _handler = new GetTileSupportLevelsHandler(
            NullLogger<GetTileSupportLevelsHandler>.Instance,
            DbFactory,
            _sightDataReader,
            _fieldOfViewService,
            new TileSupportEvaluator(new PlayerSustenanceValueReader(), new BiomeRegenerationValueReader(), new CampaignRiverReader()));
    }

    [Test, Description("Every observed tile gets one entry for the selected Force: 300 troops against 500 food is one Turn, and a stripped tile is exhausted.")]
    public async Task ShouldReturnALevelForEachObservedTileAsync()
    {
        await AddStrippedDepletionAsync(TileId(_FORCE_X + 1, _Y));

        ServiceResult<TileSupportLevels> result = await HandleAsync([_FORCE_ID]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Tiles, Is.EqualTo(new[]
            {
                new TileSupportLevelEntry { X = _FORCE_X, Y = _Y, SupportLevel = TileSupportLevel.OneTurn, Turns = 1 },
                new TileSupportLevelEntry { X = _FORCE_X + 1, Y = _Y, SupportLevel = TileSupportLevel.Exhausted, Turns = 0 }
            }));
        }
    }

    [Test, Description("Each entry carries the forecast Turns: a tile that regrows its whole base every Round feeds the Force beyond the forecast, so its Turns are null.")]
    public async Task ShouldReturnTheTurnsForecastWithRegrowthAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            CampaignBiome grassland = context.CampaignBiomes.Single(biome => biome.BiomeTypeId == BiomeType.Grassland);
            grassland.RegenerationPercent = 1m;

            await context.SaveChangesAsync();
        }

        ServiceResult<TileSupportLevels> result = await HandleAsync([_FORCE_ID]);

        Assert.That(
            result.SuccessValue!.Tiles.Single(tile => tile.X == _FORCE_X),
            Is.EqualTo(new TileSupportLevelEntry { X = _FORCE_X, Y = _Y, SupportLevel = TileSupportLevel.Plenty, Turns = null }));
    }

    [Test, Description("A tile outside live sight is never included, even a stripped one, since its level would leak where other Players' Forces have fed.")]
    public async Task ShouldLeaveOutTilesThatAreNotObservedAsync()
    {
        await AddStrippedDepletionAsync(TileId(_FORCE_X + 2, _Y));

        ServiceResult<TileSupportLevels> result = await HandleAsync([_FORCE_ID]);

        Assert.That(result.SuccessValue!.Tiles.Any(tile => tile.X == _FORCE_X + 2), Is.False);
    }

    [Test, Description("The levels are for Forces about to move, so a request naming none is a validation failure.")]
    public async Task ShouldFailValidationWithNoForceIdsAsync()
    {
        ServiceResult<TileSupportLevels> result = await HandleAsync([]);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
    }

    [Test, Description("Asking about another Player's Force is refused, so the client cannot learn the size of Forces that are not its own.")]
    public async Task ShouldFailForAnotherPlayersForceAsync()
    {
        ServiceResult<TileSupportLevels> result = await HandleAsync([_FORCE_ID, _OTHER_PLAYERS_FORCE_ID]);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
    }

    [Test, Description("A Game with no map geometry does not exist, so the read is a 404 rather than an empty answer.")]
    public async Task ShouldReturnNotFoundWhenTheGameDoesNotExistAsync()
    {
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns((CampaignMapGeometry?)null);

        ServiceResult<TileSupportLevels> result = await HandleAsync([_FORCE_ID]);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    /// <summary>
    /// A tile's id, numbered across the 10 x 10 map.
    /// </summary>
    private static int TileId(int x, int y) => 1 + (y * 10) + x;

    private async Task<ServiceResult<TileSupportLevels>> HandleAsync(int[] forceIds)
    {
        return await _handler.Handle(new GetTileSupportLevelsRequest(_GAME_ID, _PLAYER_ID, forceIds), CancellationToken.None);
    }

    /// <summary>
    /// Records a tile's Forage and Hunt as taken in full.
    /// </summary>
    private async Task AddStrippedDepletionAsync(int tileId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapDepletions.Add(new CampaignMapDepletion
        {
            CampaignMapId = tileId,
            GameId = _GAME_ID,
            ForageTaken = ScaledAmount.FromWhole(400).RawValue,
            HuntTaken = ScaledAmount.FromWhole(100).RawValue,
            FedOnRound = 1
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Has the field-of-view service report one Observer of the Player's per array, each seeing exactly the tiles given.
    /// </summary>
    private void SetFieldsOfView(params SightTerrain[][] seenTilesPerObserver)
    {
        IReadOnlyCollection<ObserverFieldOfView> fieldsOfView = [.. seenTilesPerObserver.Select((seenTiles, index) => new ObserverFieldOfView(
            new SightObserver
            {
                Id = index + 1,
                RecordType = RecordType.Unit,
                PlayerId = _PLAYER_ID,
                Location = new Coord(_FORCE_X, _Y),
                BaseSightRange = 0,
                Detection = 0
            },
            seenTiles))];

        _fieldOfViewService
            .GetPlayerFieldOfViewAsync(_GAME_ID, _PLAYER_ID, Arg.Any<CampaignMapGeometry>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyCollection<ObserverFieldOfView>>.Succeed(fieldsOfView));
    }

    private static UnitInstance BuildForce(int id, int playerId, int tileId, int troops)
    {
        return new UnitInstance
        {
            Id = id,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = tileId,
            UnitTemplateId = 1,
            InitialStrength = troops,
            HealthyCount = troops,
            InjuredCount = 0
        };
    }

    private static SightTerrain BuildSeenTile(int x, int y)
    {
        return new SightTerrain
        {
            CampaignMapId = TileId(x, y),
            X = x,
            Y = y,
            BiomeType = BiomeType.Grassland,
            GeographyType = CampaignMapGeographyType.Flat,
            DoesBlockSight = false,
            RelativeElevation = 0,
            GeographyName = "Flat",
            GeographyMovementCost = 1m,
            BiomeName = "Grassland",
            BiomeMovementCost = 1m,
            BiomeIsWater = false,
            BiomeIsTraversable = true
        };
    }
}
