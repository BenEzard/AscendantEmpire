using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;

using FluentValidation.Results;

namespace AscendantEmpire.Tests.Features.Turns.Handlers;

/// <summary>
/// Unit tests for <see cref="CreateBattleTurnQueueRequestValidator"/> — one test per validation rule.
/// </summary>
/// <remarks>
/// The validator is instantiated directly and never touches a <c>DbContext</c>, so this fixture stands up no database at all. The handler tests, which do
/// need one, live in <see cref="CreateBattleTurnQueueHandlerTests"/>.
/// </remarks>
[TestFixture]
public class CreateBattleTurnQueueRequestValidatorTests
{
    private CreateBattleTurnQueueRequestValidator _validator = null!;

    /// <summary>
    /// Builds a valid request so individual tests can selectively break one field at a time.
    /// </summary>
    private static CreateBattleTurnQueueRequest ValidRequest() => new(GameId: 1, CampaignMapTileId: 1, PlayerIdsInTurnOrder: [1, 2]);

    [SetUp]
    public void SetUp()
    {
        _validator = new CreateBattleTurnQueueRequestValidator();
    }

    [TestCase(0, Description = "GameId of zero fails the GreaterThan(0) rule.")]
    [TestCase(-1, Description = "Negative GameId fails the GreaterThan(0) rule.")]
    public void ShouldFailWhenGameIdIsNotGreaterThanZero(int invalidGameId)
    {
        CreateBattleTurnQueueRequest request = ValidRequest() with { GameId = invalidGameId };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when GameId is not greater than zero.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("Game Id is required."));
        }
    }

    [TestCase(0, Description = "CampaignMapTileId of zero fails the GreaterThan(0) rule.")]
    [TestCase(-1, Description = "Negative CampaignMapTileId fails the GreaterThan(0) rule.")]
    public void ShouldFailWhenCampaignMapTileIdIsNotGreaterThanZero(int invalidTileId)
    {
        CreateBattleTurnQueueRequest request = ValidRequest() with { CampaignMapTileId = invalidTileId };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when CampaignMapTileId is not greater than zero.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("Campaign Map Tile Id is required."));
        }
    }

    [Test, Description("An empty player list would build a turn queue that can never advance, so the validator rejects it.")]
    public void ShouldFailWhenPlayerIdsInTurnOrderIsEmpty()
    {
        CreateBattleTurnQueueRequest request = ValidRequest() with { PlayerIdsInTurnOrder = [] };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when PlayerIdsInTurnOrder is empty.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("At least one Player is required to create a Battle Turn Queue."));
        }
    }

    [Test, Description("The list is a turn order, so a repeated player id would hand somebody two turns per round.")]
    public void ShouldFailWhenPlayerIdsInTurnOrderContainsDuplicates()
    {
        // Player Id 1 appears twice — violates the Distinct check.
        CreateBattleTurnQueueRequest request = ValidRequest() with { PlayerIdsInTurnOrder = [1, 1, 2] };

        ValidationResult result = _validator.Validate(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "Validation should fail when PlayerIdsInTurnOrder contains duplicate ids.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("Duplicate Player Ids are not allowed."));
        }
    }

    [Test, Description("The positive control: without it, a rule made accidentally unsatisfiable would still pass every test above.")]
    public void ShouldSucceedWhenRequestIsValid()
    {
        CreateBattleTurnQueueRequest request = ValidRequest();

        ValidationResult result = _validator.Validate(request);

        Assert.That(result.IsValid, Is.True, $"Validation should pass for a valid request. Errors: {string.Join(", ", result.Errors.Select(e => e.ErrorMessage))}");
    }
}
