using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Turns.Handlers;

/// <summary>
/// Tests for <see cref="GetCampaignPossibleActionsHandler"/>.
/// </summary>
/// <remarks>
/// The handler delegates both of its reads — the Turn access check and the checks themselves — so it never touches a <c>DbContext</c> and this fixture
/// stands up no database. What each check actually finds is covered where the checks live; what is pinned here is the handler's own judgement about who
/// is entitled to an answer.
/// </remarks>
[TestFixture]
public class GetCampaignPossibleActionsHandlerTests
{
    private const int _GAME_ID = 7;
    private const int _PLAYER_ID = 3;
    private const int _OTHER_PLAYER_ID = 4;
    private const int _ROUND_NUMBER = 5;

    private ICampaignTurnAccessService _campaignTurnAccessService = null!;
    private IEndCampaignTurnPossibleActionService _possibleActionService = null!;

    private GetCampaignPossibleActionsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _campaignTurnAccessService = Substitute.For<ICampaignTurnAccessService>();
        _possibleActionService = Substitute.For<IEndCampaignTurnPossibleActionService>();

        // Nothing outstanding by default. The tests that care arrange their own.
        _possibleActionService
            .GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _handler = new GetCampaignPossibleActionsHandler(
            new NullLogger<GetCampaignPossibleActionsHandler>(),
            _campaignTurnAccessService,
            _possibleActionService);
    }

    /// <summary>
    /// Arranges the access service to grant access, reporting the named Player as the one taking the Turn.
    /// </summary>
    private void ArrangeOpenTurnFor(int playerIdTakingTurn)
    {
        _campaignTurnAccessService
            .GetCampaignTurnAccessAsync(_GAME_ID, _PLAYER_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignTurnAccess
            {
                Status = CampaignTurnAccessStatus.Granted,
                CurrentTurn = new CurrentCampaignTurn
                {
                    GameId = _GAME_ID,
                    PlayerId = playerIdTakingTurn,
                    CurrentRoundNumber = _ROUND_NUMBER
                }
            });
    }

    /// <summary>
    /// Arranges the access service to report the given status, with no Turn.
    /// </summary>
    private void ArrangeAccessRefused(CampaignTurnAccessStatus status, string error)
    {
        _campaignTurnAccessService
            .GetCampaignTurnAccessAsync(_GAME_ID, _PLAYER_ID, Arg.Any<CancellationToken>())
            .Returns(new CampaignTurnAccess
            {
                Status = status,
                Errors = [error]
            });
    }

    private static PossibleAction AnAction(int id, string description)
    {
        return new PossibleAction
        {
            Category = InformationCategory.Military,
            Description = description,
            Id = id,
            Coord = new Coord(1, 2),
            RecordTypeId = (int)RecordType.Unit
        };
    }

    private Task<ServiceResult<CampaignPossibleActions>> HandleAsync()
    {
        return _handler.Handle(new GetCampaignPossibleActionsRequest(_GAME_ID, _PLAYER_ID), CancellationToken.None);
    }

    [Test, Description("The Player taking the Turn is told what they have outstanding, because that is the whole point of the route — the panel they open their game to is built from it.")]
    public async Task ShouldReturnTheOutstandingActionsWhenItIsThePlayersTurnAsync()
    {
        ArrangeOpenTurnFor(_PLAYER_ID);

        _possibleActionService
            .GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>())
            .Returns([AnAction(11, "Alpha has movement remaining."), AnAction(12, "Beta has movement remaining.")]);

        ServiceResult<CampaignPossibleActions> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.IsYourTurn, Is.True);
            Assert.That(result.SuccessValue!.PossibleActions.Select(a => a.Id), Is.EqualTo(new[] { 11, 12 }));
        }
    }

    [Test, Description("The checks are asked about the round the open Turn is in, since a check that dated its findings to the wrong round would report against a board that no longer exists.")]
    public async Task ShouldAskTheChecksAboutTheOpenTurnsRoundAsync()
    {
        ArrangeOpenTurnFor(_PLAYER_ID);

        await HandleAsync();

        await _possibleActionService.Received(1).GetPossibleActionsAsync(
            Arg.Is<PossibleActionContext>(context =>
                context.GameId == _GAME_ID
                && context.PlayerId == _PLAYER_ID
                && context.CurrentRoundNumber == _ROUND_NUMBER),
            Arg.Any<CancellationToken>());
    }

    [Test, Description("Another Player's Turn answers an empty list rather than failing, because being asked to wait is ordinary in an asynchronous game and is not the caller's mistake.")]
    public async Task ShouldReturnNothingWhenItIsAnotherPlayersTurnAsync()
    {
        ArrangeOpenTurnFor(_OTHER_PLAYER_ID);

        ServiceResult<CampaignPossibleActions> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.IsYourTurn, Is.False);
            Assert.That(result.SuccessValue!.PossibleActions, Is.Empty);
        }
    }

    [Test, Description("The checks are not run at all for another Player's Turn, so a Player cannot make the server do their opponent's bookkeeping by polling.")]
    public async Task ShouldNotRunTheChecksWhenItIsAnotherPlayersTurnAsync()
    {
        ArrangeOpenTurnFor(_OTHER_PLAYER_ID);

        await HandleAsync();

        await _possibleActionService.DidNotReceive().GetPossibleActionsAsync(Arg.Any<PossibleActionContext>(), Arg.Any<CancellationToken>());
    }

    [Test, Description("A Game with no open Turn answers the same as another Player's Turn, because a Game set up but not yet started is a normal thing to open and not an error to report.")]
    public async Task ShouldReturnNothingWhenNoTurnIsOpenAsync()
    {
        ArrangeAccessRefused(CampaignTurnAccessStatus.NoTurnInProgress, "No active Campaign Turn Queue or current player was found for Game Id 7.");

        ServiceResult<CampaignPossibleActions> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.IsYourTurn, Is.False);
            Assert.That(result.SuccessValue!.PossibleActions, Is.Empty);
        }
    }

    [Test, Description("Asking about a Game the Player is not in is a Not Found, because unlike the other empty answers it is a mistake the caller should be told about rather than a state to wait out.")]
    public async Task ShouldReturnNotFoundWhenThePlayerIsNotInTheGameAsync()
    {
        const string error = "Game Id 7 was not found or Player Id 3 is not part of the game.";
        ArrangeAccessRefused(CampaignTurnAccessStatus.PlayerNotInGame, error);

        ServiceResult<CampaignPossibleActions> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors, Has.Some.EqualTo(error));
        }
    }

    [Test, Description("A failure reading the Turn is reported as a failure, so a broken lookup is never mistaken for a Player having nothing left to do.")]
    public async Task ShouldFailWhenTheTurnLookupFailsAsync()
    {
        const string error = "An error occurred while getting current campaign turn details.";
        ArrangeAccessRefused(CampaignTurnAccessStatus.Failed, error);

        ServiceResult<CampaignPossibleActions> result = await HandleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.EqualTo(error));
        }
    }

    [TestCase(0, _PLAYER_ID, Description = "A Game Id of zero fails validation before anything is read.")]
    [TestCase(_GAME_ID, 0, Description = "A Player Id of zero fails validation before anything is read.")]
    public async Task ShouldFailWhenTheRequestIsInvalidAsync(int gameId, int playerId)
    {
        ServiceResult<CampaignPossibleActions> result =
            await _handler.Handle(new GetCampaignPossibleActionsRequest(gameId, playerId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);

        await _campaignTurnAccessService.DidNotReceive().GetCampaignTurnAccessAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
