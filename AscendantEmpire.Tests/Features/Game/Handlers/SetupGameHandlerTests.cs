using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Options;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Game.Handlers;

/// <summary>
/// Guards the configured-bounds checks <see cref="SetupGameHandler"/> runs before a game is set up.
/// </summary>
/// <remarks>
/// <para>
/// These rules live in the handler rather than in <c>SetupGameRequestValidator</c> because they are read from <c>GameSettings</c>, which a shared
/// singleton validator may not hold. That makes the handler the only place they can be pinned, and the only place a test can prove a refused request
/// never reaches the service.
/// </para>
/// <para>
/// No database: every collaborator is substituted, so this sits at the lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class SetupGameHandlerTests
{
    private IGameSetupService _gameSetupService = null!;
    private SetupGameHandler _handler = null!;

    /// <summary>A game id the stubbed service hands back, so a request that passes validation has something to succeed with.</summary>
    private const int _CREATED_GAME_ID = 77;

    [SetUp]
    public void SetUp()
    {
        _gameSetupService = Substitute.For<IGameSetupService>();
        _gameSetupService
            .SetupGameAsync(Arg.Any<SetupGameRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResult<int>.Succeed(_CREATED_GAME_ID)));

        _handler = new SetupGameHandler(_gameSetupService, TestGameSettings.Create(), NullLogger<SetupGameHandler>.Instance);
    }

    /// <summary>A request that passes every check, with the one field under test overridable.</summary>
    private static SetupGameRequest ValidRequest(int battleMapSize = TestGameSettings.DEFAULT_BATTLE_MAP_SIZE)
    {
        return new SetupGameRequest(GalleryMapId: 1, GameName: "Divisibility Game", NumberOfPlayers: TestGameSettings.MINIMUM_PLAYERS_PER_GAME, BattleMapSize: battleMapSize, GenerationSeed: null);
    }

    [TestCase(19, Description = "A size one above a multiple is refused, so the slider's notches are the only sizes a game can be created on.")]
    [TestCase(20, Description = "A size one below a multiple is refused too, so the rule is a step rather than a rounding allowance.")]
    [TestCase(50, Description = "A size that is in range and looks ordinary is still refused when it is off the step, which is the case the old bounds-only check let through.")]
    public async Task Handle_BattleMapSizeOffTheIncrement_IsRefusedAndNeverReachesTheService(int battleMapSize)
    {
        ServiceResult<int> result = await _handler.Handle(ValidRequest(battleMapSize), CancellationToken.None);

        using (new FluentAssertions.Execution.AssertionScope())
        {
            result.HasSucceeded.Should().BeFalse();
            result.Errors.Should().Contain($"BattleMapSize must be divisible by {TestGameSettings.BATTLE_MAP_SIZE_INCREMENT}.");
            await _gameSetupService.DidNotReceive().SetupGameAsync(Arg.Any<SetupGameRequest>(), Arg.Any<CancellationToken>());
        }
    }

    // The default is not a case of its own because it is the maximum: the test below it covers both. Should the two part company, give it one.
    [TestCase(TestGameSettings.MINIMUM_BATTLE_MAP_SIZE, Description = "The bottom of the range is accepted, so the slider's first notch is not itself invalid.")]
    [TestCase(TestGameSettings.MAXIMUM_BATTLE_MAP_SIZE, Description = "The top of the range is accepted, so the span between the bounds divides by the increment.")]
    [TestCase(24, Description = "A size in the middle of the range is accepted, so the rule is divisibility rather than a pair of allowed endpoints.")]
    public async Task Handle_BattleMapSizeOnTheIncrement_IsAcceptedAndSetsUpTheGame(int battleMapSize)
    {
        ServiceResult<int> result = await _handler.Handle(ValidRequest(battleMapSize), CancellationToken.None);

        using (new FluentAssertions.Execution.AssertionScope())
        {
            result.HasSucceeded.Should().BeTrue();
            result.SuccessValue.Should().Be(_CREATED_GAME_ID);
            await _gameSetupService.Received(1).SetupGameAsync(Arg.Is<SetupGameRequest>(r => r.BattleMapSize == battleMapSize), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A size that is both out of range and off the step is told about both, so the caller is not sent back for a second refusal.")]
    public async Task Handle_BattleMapSizeOutOfRangeAndOffTheIncrement_ReportsBothProblems()
    {
        // One below the minimum, and deliberately not a multiple of the increment either.
        ServiceResult<int> result = await _handler.Handle(ValidRequest(TestGameSettings.MINIMUM_BATTLE_MAP_SIZE - 1), CancellationToken.None);

        using (new FluentAssertions.Execution.AssertionScope())
        {
            result.HasSucceeded.Should().BeFalse();
            result.Errors.Should().Contain($"BattleMapSize must be between {TestGameSettings.MINIMUM_BATTLE_MAP_SIZE} and {TestGameSettings.MAXIMUM_BATTLE_MAP_SIZE}.");
            result.Errors.Should().Contain($"BattleMapSize must be divisible by {TestGameSettings.BATTLE_MAP_SIZE_INCREMENT}.");
        }
    }

    [Test, Description("An unconfigured increment waves the step check through rather than dividing by zero, so a section missing it cannot take the endpoint down.")]
    public async Task Handle_IncrementNotConfigured_SkipsTheDivisibilityCheck()
    {
        IOptions<GameSettings> settings = TestGameSettings.Create();
        settings.Value.BattleMapSettings.BattleMapSizeIncrement = 0;

        SetupGameHandler handler = new(_gameSetupService, settings, NullLogger<SetupGameHandler>.Instance);

        // A size that the increment would otherwise refuse, so the test fails if the check runs at all rather than merely not throwing.
        ServiceResult<int> result = await handler.Handle(ValidRequest(TestGameSettings.MINIMUM_BATTLE_MAP_SIZE + 1), CancellationToken.None);

        result.HasSucceeded.Should().BeTrue();
    }
}
