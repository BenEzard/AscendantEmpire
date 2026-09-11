using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Tests for <see cref="RaiseSightingReports"/>, the end-of-turn process that runs the sightings step.
/// </summary>
/// <remarks>
/// No database: the sightings step itself is substituted, because what is pinned here is only how its outcome decides whether the Turn may end.
/// </remarks>
[TestFixture]
internal class RaiseSightingReportsTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _PLAYER_TURN_ID = 40;

    private IForceSightingFoldService _sightingFoldService = null!;
    private RaiseSightingReports _process = null!;

    [SetUp]
    public void SetUpProcess()
    {
        _sightingFoldService = Substitute.For<IForceSightingFoldService>();
        _process = new RaiseSightingReports(_sightingFoldService, NullLogger<RaiseSightingReports>.Instance);
    }

    private static TurnPipelineContextData EndOfTurn(int? playerTurnId)
    {
        return new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, currentRoundNumber: 1) { PlayerTurnId = playerTurnId };
    }

    [Test, Description("Raising sightings against a guessed Turn would file them under the wrong one, so a context without a Turn refuses the end of Turn instead.")]
    public async Task ShouldRefuseToEndTheTurnWhenTheContextHasNoPlayerTurnAsync()
    {
        string error = await _process.ProcessAsync(EndOfTurn(playerTurnId: null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Not.Empty);
            await _sightingFoldService.DidNotReceiveWithAnyArgs().FoldTurnAsync(default, default, default, default);
        }
    }

    [Test, Description("If the sightings cannot be raised the Turn does not end, because ending it anyway would lose them for good with nothing to raise them later.")]
    public async Task ShouldRefuseToEndTheTurnWhenTheSightingsCannotBeRaisedAsync()
    {
        _sightingFoldService.FoldTurnAsync(_GAME_ID, _PLAYER_TURN_ID, _PLAYER_ID, Arg.Any<CancellationToken>()).Returns(ServiceResultBase.Fail("The database went away."));

        string error = await _process.ProcessAsync(EndOfTurn(_PLAYER_TURN_ID));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Does.Contain("has not ended"));
            Assert.That(error, Does.Contain("The database went away."), "The step's own reason travels with the refusal.");
        }
    }

    [Test, Description("Once the sightings are raised for the ending Turn, on behalf of the Player whose Turn it is, the Turn is free to end.")]
    public async Task ShouldLetTheTurnEndWhenTheSightingsAreRaisedAsync()
    {
        _sightingFoldService.FoldTurnAsync(_GAME_ID, _PLAYER_TURN_ID, _PLAYER_ID, Arg.Any<CancellationToken>()).Returns(ServiceResultBase.Succeed());

        string error = await _process.ProcessAsync(EndOfTurn(_PLAYER_TURN_ID));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Empty);
            await _sightingFoldService.Received(1).FoldTurnAsync(_GAME_ID, _PLAYER_TURN_ID, _PLAYER_ID, Arg.Any<CancellationToken>());
        }
    }
}
