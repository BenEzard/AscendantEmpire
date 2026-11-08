using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.MapResources.GetPlayerMapResources;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests for <see cref="GetPlayerMapResourcesHandler"/>: that a Player is told about the MapResources they have discovered and can see, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler's own reads are LINQ only; sight and geometry are substituted, because the sight rules have their own tests and all
/// this handler does with them is ask whether a tile is in view. The estimate comes from the real <see cref="MapResourceEstimationRules"/>, so the number
/// the client would receive is the one asserted on.
/// </para>
/// <para>
/// The map is 10 x 10 with its block of tile ids starting at 1, so a tile's id is <c>1 + (y * 10) + x</c>. By default the Player sees only (3, 4).
/// </para>
/// </remarks>
[TestFixture]
internal sealed class GetPlayerMapResourcesHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;

    private const long _GAME_SEED = 20261108;

    private const int _MAP_WIDTH = 10;
    private const int _OFFSET = 1;

    private const int _SEEN_X = 3;
    private const int _SEEN_Y = 4;

    private const int _UNSEEN_X = 8;
    private const int _UNSEEN_Y = 9;

    /// <summary>The turn a tile the Player has explored was first seen on. Any value will do: it is the presence of one that marks the tile explored.</summary>
    private const int _FIRST_SEEN_TURN_ID = 1;

    /// <summary>How many ids each Player's tile rows are given, so two Players' rows for one tile cannot collide on the hand-assigned key.</summary>
    private const int _PLAYER_TILE_ID_RANGE = 1_000;

    private const long _TRUE_QUANTITY = 1_000_000;
    private const decimal _FUZZY_FACTOR = 0.100m;

    private ISightDataReader _sightDataReader = null!;
    private IFieldOfViewService _fieldOfViewService = null!;
    private GetPlayerMapResourcesHandler _handler = null!;

    private int _berryTypeId;
    private int _coalTypeId;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.GameIndex.Add(new GameIndex
            {
                Id = _GAME_ID,
                CampaignMapIndexId = 1,
                GameName = "Resource Read Game",
                NumberOfPlayers = 2,
                CreationStatus = GameCreationStatus.SetupComplete,
                PlayStatus = GamePlayStatus.InProgress,
                CreatedAt = DateTimeOffset.UtcNow,
                GenerationSeed = _GAME_SEED,
                BattleMapSize = 50
            });

            var berry = new MapResourceType
            {
                Code = "TestBerry",
                DisplayName = "Test Berry",
                MapResourceCategoryId = 1,
                IconPath = "/resources/food/berries_40px.png",
                IsDiscoverableByDefault = true,
                DiscoveryLikelihood = 1m
            };

            var coal = new MapResourceType
            {
                Code = "TestCoal",
                DisplayName = "Test Coal",
                MapResourceCategoryId = 1,
                IsStratified = true,
                IsDiscoverableByDefault = true,
                DiscoveryLikelihood = 1m
            };

            context.MapResourceTypes.AddRange(berry, coal);
            await context.SaveChangesAsync();

            _berryTypeId = berry.Id;
            _coalTypeId = coal.Id;
        }

        _sightDataReader = Substitute.For<ISightDataReader>();
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignMapGeometry(CampaignMapOffsetId: _OFFSET, Width: _MAP_WIDTH, Height: 10, WrapsHorizontally: false, WrapsVertically: false));

        _fieldOfViewService = Substitute.For<IFieldOfViewService>();
        SetSeenTiles(BuildSeenTile(_SEEN_X, _SEEN_Y));

        _handler = new GetPlayerMapResourcesHandler(
            NullLogger<GetPlayerMapResourcesHandler>.Instance,
            DbFactory,
            _sightDataReader,
            _fieldOfViewService);
    }

    #region What the Player is told about

    [Test, Description("A discovered resource on a tile in live sight is reported, so a berry patch the Player knows of shows on the tile they are watching.")]
    public async Task ShouldReturnADiscoveredResourceOnASeenTileAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Resources, Has.Count.EqualTo(1));
            Assert.That(result.SuccessValue.Resources.Single().X, Is.EqualTo(_SEEN_X));
            Assert.That(result.SuccessValue.Resources.Single().Y, Is.EqualTo(_SEEN_Y));
        }
    }

    [Test, Description("A discovered resource outside live sight is still reported, because what the Player found stays on their map once they move on.")]
    public async Task ShouldStillReportADiscoveredResourceOnAnUnwatchedTileAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _UNSEEN_X, _UNSEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        PlayerMapResourceEntry entry = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.X, Is.EqualTo(_UNSEEN_X), "An unwatched tile is placed from the map's geometry rather than from what is seen.");
            Assert.That(entry.Y, Is.EqualTo(_UNSEEN_Y));
            Assert.That(entry.IsObserved, Is.False);
        }
    }

    [Test, Description("An unwatched resource comes with no quantity, since a quantity moves as others extract it and would leak what they have been doing.")]
    public async Task ShouldReportNoQuantityForAnUnwatchedResourceAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _UNSEEN_X, _UNSEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources.Single().EstimatedQuantity, Is.Null);
    }

    [Test, Description("A resource on a tile in live sight is marked observed, which is what tells the tile it may draw a quantity.")]
    public async Task ShouldMarkAResourceOnASeenTileAsObservedAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources.Single().IsObserved, Is.True);
    }

    [Test, Description("A Player whose Observers can see nothing still gets what they have discovered, just without quantities.")]
    public async Task ShouldReportDiscoveriesWhenThePlayerSeesNothingAsync()
    {
        SetSeenTiles();
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Resources, Has.Count.EqualTo(1));
            Assert.That(result.SuccessValue.Resources.Single().IsObserved, Is.False);
        }
    }

    [Test, Description("An undiscovered resource on a seen tile is left out, so standing next to uranium does not reveal it.")]
    public async Task ShouldOmitAResourceThePlayerHasNotDiscoveredAsync()
    {
        await PlaceInstanceAsync(_berryTypeId, _SEEN_X, _SEEN_Y);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources, Is.Empty);
    }

    [Test, Description("A discovery on ground the Player has never been to is withheld, so a new Game does not hand them the whole map's resources.")]
    public async Task ShouldOmitADiscoveryOnAnUnexploredTileAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _UNSEEN_X, _UNSEEN_Y, _PLAYER_ID, isExplored: false);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources, Is.Empty);
    }

    [Test, Description("A tile in sight counts as seen before the explored stamp is written, so a patch beside a start position shows on the Game's first turn.")]
    public async Task ShouldReportADiscoveryOnAnUnstampedTileInSightAsync()
    {
        // What a new Game looks like on its first read: the start tile is in plain sight, and FirstSeenOnTurnId is written by the map read, not by setup.
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID, isExplored: false);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        PlayerMapResourceEntry entry = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.IsObserved, Is.True);
            Assert.That(entry.EstimatedQuantity, Is.Not.Null, "A tile in sight carries an estimate, stamp or no stamp.");
        }
    }

    [Test, Description("Exploring a tile is enough to be told what was discovered on it, without anything of the Player's still watching it.")]
    public async Task ShouldReportADiscoveryOnAnExploredButUnwatchedTileAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _UNSEEN_X, _UNSEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources, Has.Count.EqualTo(1));
    }

    [Test, Description("Another Player's discovery is not the caller's, so one Player finding a seam tells the others nothing.")]
    public async Task ShouldOmitAnotherPlayersDiscoveryAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _OTHER_PLAYER_ID);

        // The caller has explored the tile themselves, so it is the missing discovery that withholds the resource and not the missing exploration.
        await AddPlayerTileAsync(_SEEN_X, _SEEN_Y, _PLAYER_ID, isExplored: true);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources, Is.Empty);
    }
    #endregion

    #region What each entry carries

    [Test, Description("An entry carries the type's code, name and icon, so the tile can draw it without a second request.")]
    public async Task ShouldDescribeTheResourceSoTheTileCanDrawItAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        PlayerMapResourceEntry entry = result.SuccessValue!.Resources.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Code, Is.EqualTo("TestBerry"));
            Assert.That(entry.DisplayName, Is.EqualTo("Test Berry"));
            Assert.That(entry.IconPath, Is.EqualTo("/resources/food/berries_40px.png"));
        }
    }

    [Test, Description("The quantity reported is the Player's fuzzed estimate, never the instance's true quantity.")]
    public async Task ShouldReportAnEstimateRatherThanTheTrueQuantityAsync()
    {
        int instanceId = await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        long rawEstimate = MapResourceEstimationRules.Estimate(_GAME_SEED, instanceId, _PLAYER_ID, _TRUE_QUANTITY, _FUZZY_FACTOR)!.Value;

        Assert.That(result.SuccessValue!.Resources.Single().EstimatedQuantity, Is.EqualTo(ScaledAmount.FromRaw(rawEstimate).ToDecimal()));
    }

    [Test, Description("The estimate leaves in whole units, so no client has to know the scale the quantity is stored at.")]
    public async Task ShouldReportTheEstimateInWholeUnitsAsync()
    {
        await PlaceAndDiscoverAsync(_berryTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        // Within the fuzzy factor of the true quantity descaled: a raw figure would be a thousand times larger than this range.
        decimal trueWhole = ScaledAmount.FromRaw(_TRUE_QUANTITY).ToDecimal();

        Assert.That(
            result.SuccessValue!.Resources.Single().EstimatedQuantity,
            Is.EqualTo(trueWhole).Within(trueWhole * _FUZZY_FACTOR));
    }

    [Test, Description("A stratified instance reports no quantity, because its quantity lives in layers a Player has to survey.")]
    public async Task ShouldReportNoQuantityForAStratifiedInstanceAsync()
    {
        await PlaceAndDiscoverAsync(_coalTypeId, _SEEN_X, _SEEN_Y, _PLAYER_ID, quantity: null, fuzzyFactor: null);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.SuccessValue!.Resources.Single().EstimatedQuantity, Is.Null);
    }
    #endregion

    #region Failures

    [Test, Description("A Game with no map is reported as not found rather than answered with an empty map.")]
    public async Task ShouldFailWhenTheGameIsNotFoundAsync()
    {
        _sightDataReader
            .GetGeometryAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>())
            .Returns((CampaignMapGeometry?)null);

        ServiceResult<PlayerMapResources> result = await HandleAsync();

        Assert.That(result.HasSucceeded, Is.False);
    }
    #endregion

    #region Helpers

    private async Task<ServiceResult<PlayerMapResources>> HandleAsync()
    {
        return await _handler.Handle(new GetPlayerMapResourcesRequest(_GAME_ID, _PLAYER_ID), CancellationToken.None);
    }

    /// <summary>
    /// Adds an instance on the tile, with the default single-quantity values unless told otherwise.
    /// </summary>
    /// <returns>The instance's id.</returns>
    private async Task<int> PlaceInstanceAsync(
        int mapResourceTypeId,
        int x,
        int y,
        long? quantity = _TRUE_QUANTITY,
        decimal? fuzzyFactor = _FUZZY_FACTOR)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var instance = new MapResourceInstance
        {
            GameId = _GAME_ID,
            CampaignMapId = _OFFSET + (y * _MAP_WIDTH) + x,
            MapResourceTypeId = mapResourceTypeId,
            Slot = 1,
            Quantity = quantity,
            EstimationFuzzyFactor = fuzzyFactor,
            CreatedOnRound = 0
        };

        context.MapResourceInstances.Add(instance);
        await context.SaveChangesAsync();

        return instance.Id;
    }

    /// <summary>
    /// Adds an instance on the tile, records that the Player knows of it, and — unless <paramref name="isExplored"/> says otherwise — that they have been to
    /// the tile.
    /// </summary>
    /// <remarks>
    /// Explored by default because that is the ordinary state of a tile a Player has found something on. Pass <see langword="false"/> to reproduce what game
    /// setup actually leaves behind: discovery is seeded map-wide, so a Player starts out knowing of instances on ground they have never visited.
    /// </remarks>
    /// <returns>The instance's id.</returns>
    private async Task<int> PlaceAndDiscoverAsync(
        int mapResourceTypeId,
        int x,
        int y,
        int playerId,
        long? quantity = _TRUE_QUANTITY,
        decimal? fuzzyFactor = _FUZZY_FACTOR,
        bool isExplored = true)
    {
        int instanceId = await PlaceInstanceAsync(mapResourceTypeId, x, y, quantity, fuzzyFactor);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerKnownMapResources.Add(new PlayerKnownMapResource
        {
            GameId = _GAME_ID,
            PlayerId = playerId,
            MapResourceInstanceId = instanceId,
            DiscoveredOnRound = 0
        });

        await context.SaveChangesAsync();

        await AddPlayerTileAsync(x, y, playerId, isExplored);

        return instanceId;
    }

    /// <summary>
    /// Adds the Player's own map row for the tile, either explored or never visited.
    /// </summary>
    /// <remarks>
    /// Navigations are left null: the in-memory provider enforces no foreign keys, and the handler reads only the scalar columns — the same shortcut the
    /// river tiles in <c>GetTileSustenanceHandlerTests</c> take.
    /// <para>
    /// The key is set by hand because <c>PlayerCampaignMapId</c> is <c>ValueGeneratedNever</c>: production reserves a range of ids per Player, and this
    /// stands in for that with one id per Player and tile.
    /// </para>
    /// </remarks>
    private async Task AddPlayerTileAsync(int x, int y, int playerId, bool isExplored)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int campaignMapId = _OFFSET + (y * _MAP_WIDTH) + x;

        context.PlayerCampaignMaps.Add(new PlayerCampaignMap
        {
            PlayerCampaignMapId = (playerId * _PLAYER_TILE_ID_RANGE) + campaignMapId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapId = campaignMapId,
            XCoord = x,
            YCoord = y,
            BiomeTypeId = BiomeType.Grassland,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            FirstSeenOnTurnId = isExplored ? _FIRST_SEEN_TURN_ID : null,
            CampaignMap = null!,
            Biome = null!,
            Geography = null!,
            Game = null!,
            Player = null!
        });

        await context.SaveChangesAsync();
    }

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

    private static SightTerrain BuildSeenTile(int x, int y)
    {
        return new SightTerrain
        {
            CampaignMapId = _OFFSET + (y * _MAP_WIDTH) + x,
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
            GeographyIsWater = false,
            BiomeIsTraversable = true,
            GeographyIsTraversable = true
        };
    }
    #endregion
}
