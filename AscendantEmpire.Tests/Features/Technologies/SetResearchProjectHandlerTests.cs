using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests choosing a research project, and the unlock it performs on the way.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the handler reads two rows and writes them back. The cases worth the most are the <b>unlock</b> - directed research requires Unlocked, and
/// this handler is the only thing in the codebase that ever writes that status - and the <b>refusals</b>, since every one of them is a rule stated nowhere
/// else in code.
/// </para>
/// <para>
/// What this tier cannot check is <c>CK_PlayerTechnology_StatusStamps</c>, which only exists on SQL Server.
/// <see cref="SetResearchProjectConstraintTests"/> covers that on a real database.
/// </para>
/// </remarks>
[TestFixture]
internal class SetResearchProjectHandlerTests : ResearchHandlerTestBase
{
    private SetResearchProjectHandler _handler = null!;

    [SetUp]
    public void SetUpHandler()
    {
        _handler = new SetResearchProjectHandler(
            NullLogger<SetResearchProjectHandler>.Instance,
            DbFactory,
            new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory));
    }

    // --- Choosing a free technology ---------------------------------------------------

    [Test, Description("A free Available technology unlocks as it is chosen, because directed research cannot begin until the acquisition cost is settled.")]
    public async Task ShouldUnlockAFreeTechnologyWhenItIsChosen()
    {
        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Available);

        await ChooseAsync("Hunting");

        PlayerTechnology row = await RowAsync("Hunting");

        Assert.Multiple(() =>
        {
            Assert.That(row.Status, Is.EqualTo(TechnologyStatus.Unlocked));

            // Stamping the Round is the unlock event - there is no separate UnlockedOnRound column - and a free technology still stamps one, because the
            // cost was zero rather than unpaid.
            Assert.That(row.AcquisitionPaidOnRound, Is.EqualTo(FIRST_ROUND));
            Assert.That(row.DiscoveredOnRound, Is.Null);
        });
    }

    [Test, Description("The chosen technology becomes the current project, which is the column the end-of-turn research step will read.")]
    public async Task ShouldRecordTheChosenTechnologyAsTheCurrentProject()
    {
        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Available);

        await ChooseAsync("Hunting");

        PlayerResearchState state = await ResearchStateAsync();

        Assert.That(state.CurrentTechnologyId, Is.EqualTo(await TechnologyIdAsync("Hunting")));
    }

    [Test, Description("The Round an unlock was stamped with is the game's current Round, not the first, so a mid-game choice records when it happened.")]
    public async Task ShouldStampTheGamesCurrentRound()
    {
        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Available);
        await SetCurrentRoundAsync(9);

        await ChooseAsync("Hunting");

        Assert.That((await RowAsync("Hunting")).AcquisitionPaidOnRound, Is.EqualTo(9));
    }

    // --- Choosing an already-unlocked technology --------------------------------------

    [Test, Description("Re-choosing an Unlocked technology does not re-stamp the Round, because the cost was settled when it was first paid.")]
    public async Task ShouldNotRestampTheRoundOfAnAlreadyUnlockedTechnology()
    {
        await SeedCatalogueAsync([Technology("Hunting"), Technology("Language")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Unlocked, acquisitionPaidOnRound: 2);
        await SetStatusAsync("Language", TechnologyStatus.Available);
        await SetCurrentRoundAsync(9);

        // Switched away and back, which is what a Player changing their mind twice does.
        await ChooseAsync("Language");
        await ChooseAsync("Hunting");

        PlayerTechnology row = await RowAsync("Hunting");

        Assert.Multiple(() =>
        {
            Assert.That(row.Status, Is.EqualTo(TechnologyStatus.Unlocked));
            Assert.That(row.AcquisitionPaidOnRound, Is.EqualTo(2));
        });
    }

    // --- Clearing ---------------------------------------------------------------------

    [Test, Description("A null technology clears the project rather than being a no-op, because researching nothing is a state a Player may return to.")]
    public async Task ShouldClearTheProjectWhenNoTechnologyIsNamed()
    {
        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Available);
        await ChooseAsync("Hunting");

        ServiceResultBase result = await _handler.Handle(new SetResearchProjectRequest(GAME_ID, PlayerId, null), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
            Assert.That((await ResearchStateAsync()).CurrentTechnologyId, Is.Null);
        });
    }

    [Test, Description("Clearing leaves the technology unlocked, because the cost it paid is not refunded by changing your mind about studying it.")]
    public async Task ShouldLeaveATechnologyUnlockedAfterTheProjectIsCleared()
    {
        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Hunting", TechnologyStatus.Available);
        await ChooseAsync("Hunting");

        await _handler.Handle(new SetResearchProjectRequest(GAME_ID, PlayerId, null), CancellationToken.None);

        Assert.That((await RowAsync("Hunting")).Status, Is.EqualTo(TechnologyStatus.Unlocked));
    }

    // --- Refusals ---------------------------------------------------------------------

    [Test, Description("A Locked technology is refused, because its prerequisites are unmet and nothing about choosing it should change that.")]
    public async Task ShouldRefuseALockedTechnology()
    {
        await SeedCatalogueAsync([Technology("Root"), Technology("Child") with { Prerequisites = [Mandatory("Root")] }]);
        await SeedPlayerRowsAsync();

        ServiceResultBase result = await ChooseAsync("Child", expectSuccess: false);

        Assert.Multiple(async () =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors.Single(), Does.Contain("prerequisites"));
            Assert.That((await RowAsync("Child")).Status, Is.EqualTo(TechnologyStatus.Locked));
            Assert.That((await ResearchStateAsync()).CurrentTechnologyId, Is.Null);
        });
    }

    [Test, Description("A Discovered technology is refused, because there is nothing left in it to research.")]
    public async Task ShouldRefuseADiscoveredTechnology()
    {
        await SeedCatalogueAsync([Technology("Fire")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Fire", TechnologyStatus.Discovered, discoveredOnRound: 1);

        ServiceResultBase result = await ChooseAsync("Fire", expectSuccess: false);

        Assert.Multiple(async () =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors.Single(), Does.Contain("already been discovered"));
            Assert.That((await ResearchStateAsync()).CurrentTechnologyId, Is.Null);
        });
    }

    [Test, Description("A technology with an acquisition cost is refused rather than unlocked for free, because waiving a cost would be a rule invented here.")]
    public async Task ShouldRefuseATechnologyThatCarriesAnAcquisitionCost()
    {
        await SeedAssetAsync("Flint", "Flint");
        await SeedCatalogueAsync(
        [
            Technology("Knapping") with { AcquisitionCosts = [Cost("Flint", 20)] }
        ]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Knapping", TechnologyStatus.Available);

        ServiceResultBase result = await ChooseAsync("Knapping", expectSuccess: false);

        Assert.Multiple(async () =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));

            // The cost is named in the refusal, because "it costs something" without saying what leaves the Player nothing to act on.
            Assert.That(result.Errors.Single(), Does.Contain("20 x Flint"));
            Assert.That((await RowAsync("Knapping")).Status, Is.EqualTo(TechnologyStatus.Available));
            Assert.That((await ResearchStateAsync()).CurrentTechnologyId, Is.Null);
        });
    }

    [Test, Description("A technology the Player has no row for is reported as not found, which is how a technology seeded after game setup presents.")]
    public async Task ShouldReturnNotFoundWhenThePlayerHasNoRowForTheTechnology()
    {
        await SeedCatalogueAsync([Technology("Seeded.Later")]);

        // Deliberately no SeedPlayerRowsAsync: the catalogue has the technology, this Player does not have a row for it.
        ServiceResultBase result = await ChooseAsync("Seeded.Later", expectSuccess: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
            Assert.That(result.Errors.Single(), Does.Contain("reconcile"));
        });
    }

    [Test, Description("A Player with no research state is reported as not found, because the row is written at setup and its absence is a broken game.")]
    public async Task ShouldReturnNotFoundWhenThePlayerHasNoResearchState()
    {
        int otherPlayerId = await AddPlayerAsync("Stateless");

        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync(otherPlayerId);

        ServiceResultBase result = await _handler.Handle(
            new SetResearchProjectRequest(GAME_ID, otherPlayerId, await TechnologyIdAsync("Hunting")),
            CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("One Player's choice never touches another's row, since the per-Player table is the only thing keeping two empires' research apart.")]
    public async Task ShouldNotUnlockAnotherPlayersRow()
    {
        int secondPlayerId = await AddPlayerAsync("Second");

        await SeedCatalogueAsync([Technology("Hunting")]);
        await SeedPlayerRowsAsync();
        await SeedPlayerRowsAsync(secondPlayerId);
        await SeedResearchStateAsync(secondPlayerId);
        await SetStatusAsync("Hunting", TechnologyStatus.Available);
        await SetStatusAsync("Hunting", TechnologyStatus.Available, playerId: secondPlayerId);

        await ChooseAsync("Hunting");

        Assert.Multiple(async () =>
        {
            Assert.That((await RowAsync("Hunting")).Status, Is.EqualTo(TechnologyStatus.Unlocked));
            Assert.That((await RowAsync("Hunting", secondPlayerId)).Status, Is.EqualTo(TechnologyStatus.Available));
            Assert.That((await ResearchStateAsync(secondPlayerId)).CurrentTechnologyId, Is.Null);
        });
    }

    // --- Helpers ----------------------------------------------------------------------

    private async Task<ServiceResultBase> ChooseAsync(string code, bool expectSuccess = true)
    {
        ServiceResultBase result = await _handler.Handle(
            new SetResearchProjectRequest(GAME_ID, PlayerId, await TechnologyIdAsync(code)),
            CancellationToken.None);

        if (expectSuccess)
        {
            Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));
        }

        return result;
    }
}
