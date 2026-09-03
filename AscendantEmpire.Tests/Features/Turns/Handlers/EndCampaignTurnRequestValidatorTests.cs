using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Enums.Turns;

namespace AscendantEmpire.Tests.Features.Turns.Handlers;

/// <summary>
/// Unit tests for <see cref="EndCampaignTurnRequestValidator"/> — one test per validation rule.
/// </summary>
/// <remarks>
/// The validator is instantiated directly and never touches a <c>DbContext</c>, so this fixture stands up no database at all. The handler tests, which do
/// need one, live in <see cref="EndCampaignTurnHandlerTests"/>.
/// </remarks>
[TestFixture]
public class EndCampaignTurnRequestValidatorTests
{
    private EndCampaignTurnRequestValidator _validator = null!;

    /// <summary>
    /// Builds a valid request so individual test cases can selectively break one field at a time.
    /// </summary>
    private static EndCampaignTurnRequest ValidRequest() => new(GameId: 1, PlayerId: 1, EndTurnMode: EndTurnMode.CheckForActions);

    [SetUp]
    public void SetUp()
    {
        _validator = new EndCampaignTurnRequestValidator();
    }

    // ── GameId ────────────────────────────────────────────────────────────────

    [TestCase(0, Description = "GameId of zero fails the GreaterThan(0) rule.")]
    [TestCase(-1, Description = "Negative GameId fails the GreaterThan(0) rule.")]
    public void ShouldFailWhenGameIdIsNotGreaterThanZero(int invalidGameId)
    {
        EndCampaignTurnRequest request = ValidRequest() with { GameId = invalidGameId };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when GameId is not greater than zero.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("Game Id is required."));
        }
    }

    // ── PlayerId ──────────────────────────────────────────────────────────────

    [TestCase(0, Description = "PlayerId of zero fails the GreaterThan(0) rule.")]
    [TestCase(-1, Description = "Negative PlayerId fails the GreaterThan(0) rule.")]
    public void ShouldFailWhenPlayerIdIsNotGreaterThanZero(int invalidPlayerId)
    {
        EndCampaignTurnRequest request = ValidRequest() with { PlayerId = invalidPlayerId };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when PlayerId is not greater than zero.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("Player Id is required."));
        }
    }

    // ── EndTurnMode ───────────────────────────────────────────────────────────

    [TestCase(-1, Description = "Value -1 is not a defined EndTurnMode member.")]
    [TestCase(99, Description = "Value 99 is not a defined EndTurnMode member.")]
    public void ShouldFailWhenEndTurnModeIsNotAValidEnumValue(int invalidMode)
    {
        EndCampaignTurnRequest request = ValidRequest() with { EndTurnMode = (EndTurnMode)invalidMode };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when EndTurnMode is not a valid enum value.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("End Turn Mode is not valid."));
        }
    }

    // ── Valid request ─────────────────────────────────────────────────────────

    [TestCase(EndTurnMode.CheckForActions, Description = "CheckForActions is a valid EndTurnMode.")]
    [TestCase(EndTurnMode.ForceEnd, Description = "ForceEnd is a valid EndTurnMode.")]
    [TestCase(EndTurnMode.Developer_AllAboutMe, Description = "Developer_AllAboutMe is a valid EndTurnMode.")]
    public void ShouldSucceedWhenRequestIsValid(EndTurnMode validMode)
    {
        EndCampaignTurnRequest request = ValidRequest() with { EndTurnMode = validMode };

        ValidationResult result = _validator.Validate(request);

        Assert.That(result.IsValid, Is.True,
            $"Validation should pass for EndTurnMode.{validMode}. Errors: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }
}
