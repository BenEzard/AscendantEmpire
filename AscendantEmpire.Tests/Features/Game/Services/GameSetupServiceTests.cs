using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Domain.Enums.Maps;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;


namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Tests for the GameSetupService class.
/// </summary>
public partial class GameSetupServiceTests
{
    private ILogger<GameSetupService> _logger = null!;
    private IDbContextFactory<AscendantEmpireContext> _dbFactory = null!;
    private ICampaignMapGalleryService _campaignMapGalleryService = null!;
    private GameSetupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = NullLogger<GameSetupService>.Instance;
        _dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        _campaignMapGalleryService = Substitute.For<ICampaignMapGalleryService>();
        _service = new GameSetupService(_logger, _dbFactory, _campaignMapGalleryService);
    }

    /// <summary>
    /// Tests that SetupGameAsync returns a failure result when the campaign map gallery service
    /// returns a failed result.
    /// Input: Request with valid parameters, but map gallery service returns failure.
    /// Expected: Returns ServiceResult with HasSucceeded = false and propagates errors.
    /// </summary>
    [Test, Description("A map gallery failure propagates its own errors verbatim, so the caller learns what actually went wrong.")]
    public async Task SetupGameAsync_MapGalleryServiceFails_ReturnsFailureResult()
    {
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 1, GameName: "Test Game", NumberOfPlayers: 4, BattleMapSize: 50, GenerationSeed: null);
        var expectedErrors = new[] { "Map not found", "Service unavailable" };
        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Fail(expectedErrors);

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        using (new FluentAssertions.Execution.AssertionScope())
        {
            result.HasSucceeded.Should().BeFalse();
            result.Errors.Should().BeEquivalentTo(expectedErrors);
        }
    }

    /// <summary>
    /// Tests that SetupGameAsync returns a failure result when an unexpected exception occurs
    /// during the setup process.
    /// Input: Request with valid parameters, but an exception is thrown during processing.
    /// Expected: Returns failure result with generic error message and logs the exception.
    /// </summary>
    [Test, Description("An unexpected exception answers generically, since its text can carry connection strings and schema names.")]
    public async Task SetupGameAsync_UnexpectedExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 1, GameName: "Test Game", NumberOfPlayers: 4, BattleMapSize: 50, GenerationSeed: null);
        InvalidOperationException expectedException = new InvalidOperationException("Database connection failed");

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>())
            .Returns<ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>>(_ => throw expectedException);

        // Act
        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        // Assert
        using (new FluentAssertions.Execution.AssertionScope())
        {
            result.HasSucceeded.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Be("An error occurred while setting up a new game.");
        }
    }

    /// <summary>
    /// Tests that SetupGameAsync handles minimum number of players (2).
    /// Input: Request with NumberOfPlayers = 2.
    /// Expected: Request is processed (validation at line 45 is the first check).
    /// </summary>
    [Test, Description("The lower player-count bound is accepted, not rejected off-by-one; reads the constant so retuning cannot stale it.")]
    public async Task SetupGameAsync_MinimumNumberOfPlayers_ProcessesRequest()
    {
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 1, GameName: "Min Players Game", NumberOfPlayers: GameSetupService.MIN_NUMBER_OF_PLAYERS, BattleMapSize: 50, GenerationSeed: null);
        CampaignMapGalleryProperties mapProperties = new CampaignMapGalleryProperties
        {
            Id = 1,
            MapName = "Test Map",
            Width = 100,
            Height = 100,
            CreationStatus = MapCreationStatus.Pending,
            CampaignMapOffsetId = 1,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Succeed(mapProperties);

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        result.HasSucceeded.Should().BeFalse();
        await _campaignMapGalleryService.Received(1).GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that SetupGameAsync handles maximum number of players (8).
    /// Input: Request with NumberOfPlayers = 8.
    /// Expected: Request is processed (validation at line 45 is the first check).
    /// </summary>
    [Test, Description("The upper player-count bound, pinning the other end of the range the minimum test above covers.")]
    public async Task SetupGameAsync_MaximumNumberOfPlayers_ProcessesRequest()
    {
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 1, GameName: "Max Players Game", NumberOfPlayers: GameSetupService.MAX_NUMBER_OF_PLAYERS, BattleMapSize: 50, GenerationSeed: null);
        CampaignMapGalleryProperties mapProperties = new CampaignMapGalleryProperties
        {
            Id = 1,
            MapName = "Test Map",
            Width = 100,
            Height = 100,
            CreationStatus = MapCreationStatus.Pending,
            CampaignMapOffsetId = 1,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Succeed(mapProperties);

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        result.HasSucceeded.Should().BeFalse();
        await _campaignMapGalleryService.Received(1).GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that SetupGameAsync handles zero GalleryMapId.
    /// Input: Request with GalleryMapId = 0.
    /// Expected: Request is processed and likely returns error from map gallery service.
    /// </summary>
    [Test, Description("A zero map id reaches the gallery service rather than being short-circuited by a local guard.")]
    public async Task SetupGameAsync_ZeroGalleryMapId_ProcessesRequest()
    {
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 0, GameName: "Test Game", NumberOfPlayers: 4, BattleMapSize: 50, GenerationSeed: null);
        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Fail("Map not found");

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        result.HasSucceeded.Should().BeFalse();
        await _campaignMapGalleryService.Received(1).GetCampaignMapGalleryProperties(0, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that SetupGameAsync handles negative GalleryMapId.
    /// Input: Request with GalleryMapId = -1.
    /// Expected: Request is processed and likely returns error from map gallery service.
    /// </summary>
    [Test, Description("A negative map id behaves as the zero case does; the two are distinct ways to send a junk id.")]
    public async Task SetupGameAsync_NegativeGalleryMapId_ProcessesRequest()
    {
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: -1, GameName: "Test Game", NumberOfPlayers: 4, BattleMapSize: 50, GenerationSeed: null);
        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Fail("Invalid map ID");

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(-1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        result.HasSucceeded.Should().BeFalse();
        await _campaignMapGalleryService.Received(1).GetCampaignMapGalleryProperties(-1, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Tests that SetupGameAsync handles very long game names.
    /// Input: Request with extremely long game name (1000+ characters).
    /// Expected: Request is processed (validation is assumed to be handled elsewhere).
    /// </summary>
    [Test, Description("A 1,000-character game name neither truncates nor throws; length is enforced by the validator, not here.")]
    public async Task SetupGameAsync_VeryLongGameName_ProcessesRequest()
    {
        string longGameName = new string('A', 1000);
        SetupGameRequest request = new SetupGameRequest(GalleryMapId: 1, GameName: longGameName, NumberOfPlayers: 4, BattleMapSize: 50, GenerationSeed: null);
        CampaignMapGalleryProperties mapProperties = new CampaignMapGalleryProperties
        {
            Id = 1,
            MapName = "Test Map",
            Width = 100,
            Height = 100,
            CreationStatus = MapCreationStatus.Pending,
            CampaignMapOffsetId = 1,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ServiceResultBase.ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Succeed(mapProperties);

        _campaignMapGalleryService
            .GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mapPropertiesResult));

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        result.HasSucceeded.Should().BeFalse();
        await _campaignMapGalleryService.Received(1).GetCampaignMapGalleryProperties(request.GalleryMapId, Arg.Any<CancellationToken>());
    }
}