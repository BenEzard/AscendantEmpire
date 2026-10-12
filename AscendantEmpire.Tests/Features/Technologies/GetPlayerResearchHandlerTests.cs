using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.BackendContracts.Technologies.GetPlayerResearch;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests what a Player's research read returns, with the emphasis on which statuses reach the list at all.
/// </summary>
/// <remarks>
/// <para>
/// In-memory: the handler is a pair of LINQ projections over the context. The cases worth the most are the <b>status filter</b>, since a Locked technology
/// reaching a client would offer a choice the server would then refuse; and that the figures served are the <b>Player's own</b> rather than the catalogue's,
/// which is the distinction the whole per-Player table exists for.
/// </para>
/// <para>
/// The database's own check constraints are not exercised here - the in-memory provider has none - so the stamps this fixture writes are the ones a real
/// unlock would write rather than whatever happens to satisfy the assertion.
/// </para>
/// </remarks>
[TestFixture]
internal class GetPlayerResearchHandlerTests : ResearchHandlerTestBase
{
    private GetPlayerResearchHandler _handler = null!;

    [SetUp]
    public void SetUpHandler()
    {
        _handler = new GetPlayerResearchHandler(NullLogger<GetPlayerResearchHandler>.Instance, DbFactory);
    }

    // --- Which statuses reach the list -----------------------------------------------

    [Test, Description("Available and Unlocked both come back, because both are technologies the Player can put research into.")]
    public async Task ShouldReturnAvailableAndUnlockedTechnologies()
    {
        await SeedCatalogueAsync([Technology("Available"), Technology("Unlocked")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Available", TechnologyStatus.Available);
        await SetStatusAsync("Unlocked", TechnologyStatus.Unlocked, acquisitionPaidOnRound: 1);

        PlayerResearchOverview overview = await GetAsync();

        Assert.That(overview.Researchable.Select(technology => technology.Code), Is.EquivalentTo(new[] { "Available", "Unlocked" }));
    }

    [Test, Description("A Locked technology is excluded, because offering one would invite a choice the write endpoint would then refuse.")]
    public async Task ShouldExcludeALockedTechnology()
    {
        await SeedCatalogueAsync([Technology("Root"), Technology("Child") with { Prerequisites = [Mandatory("Root")] }]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Root", TechnologyStatus.Available);

        PlayerResearchOverview overview = await GetAsync();

        Assert.That(overview.Researchable.Select(technology => technology.Code), Is.EqualTo(new[] { "Root" }));
    }

    [Test, Description("A Discovered technology is excluded, because there is nothing left to research in one that is finished.")]
    public async Task ShouldExcludeADiscoveredTechnology()
    {
        await SeedCatalogueAsync([Technology("Done"), Technology("Open")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Done", TechnologyStatus.Discovered, discoveredOnRound: 1);
        await SetStatusAsync("Open", TechnologyStatus.Available);

        PlayerResearchOverview overview = await GetAsync();

        Assert.That(overview.Researchable.Select(technology => technology.Code), Is.EqualTo(new[] { "Open" }));
    }

    [Test, Description("A Player with everything locked gets an empty list rather than a failure, which is the real state of a game before any discovery.")]
    public async Task ShouldReturnAnEmptyListWhenNothingIsResearchable()
    {
        await SeedCatalogueAsync([Technology("Root")]);
        await SeedPlayerRowsAsync();

        PlayerResearchOverview overview = await GetAsync();

        Assert.That(overview.Researchable, Is.Empty);
    }

    // --- Whose figures ----------------------------------------------------------------

    [Test, Description("The requirement figures come off the Player's own row, not the catalogue, which is the distinction the per-Player table exists for.")]
    public async Task ShouldServeThePlayersOwnRequirementsRatherThanTheCatalogues()
    {
        await SeedCatalogueAsync([Technology("Scaled") with { DirectedRequired = 100 }]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Scaled", TechnologyStatus.Available);

        // As a speed multiplier of 150% would have left it at game setup.
        await SetRequirementsAsync("Scaled", directedRequired: 150);

        ResearchableTechnology technology = (await GetAsync()).Researchable.Single();

        Assert.That(technology.DirectedRequired, Is.EqualTo(150));
    }

    [Test, Description("An unused bucket stays null rather than becoming zero, because an unused bucket has no requirement at all rather than one of zero.")]
    public async Task ShouldKeepAnUnusedBucketNull()
    {
        await SeedCatalogueAsync([Technology("DirectedOnly")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("DirectedOnly", TechnologyStatus.Available);

        ResearchableTechnology technology = (await GetAsync()).Researchable.Single();

        Assert.Multiple(() =>
        {
            Assert.That(technology.DirectedRequired, Is.EqualTo(100));
            Assert.That(technology.ExperienceRequired, Is.Null);
            Assert.That(technology.DiffusionRequired, Is.Null);
        });
    }

    [Test, Description("Points accumulated so far travel with the requirement, so a client can show progress without a second call.")]
    public async Task ShouldServeThePointsAccumulatedSoFar()
    {
        await SeedCatalogueAsync([Technology("Partial")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Partial", TechnologyStatus.Unlocked, acquisitionPaidOnRound: 1);
        await SetPointsAsync("Partial", directedPoints: 40);

        ResearchableTechnology technology = (await GetAsync()).Researchable.Single();

        Assert.That(technology.DirectedPoints, Is.EqualTo(40));
    }

    [Test, Description("One Player's rows are never served to another, since the read is the only thing standing between two empires' research.")]
    public async Task ShouldNotLeakAnotherPlayersRows()
    {
        int secondPlayerId = await AddPlayerAsync("Second");

        await SeedCatalogueAsync([Technology("Mine"), Technology("Theirs")]);
        await SeedPlayerRowsAsync();
        await SeedPlayerRowsAsync(secondPlayerId);
        await SeedResearchStateAsync(secondPlayerId);

        await SetStatusAsync("Mine", TechnologyStatus.Available);
        await SetStatusAsync("Theirs", TechnologyStatus.Available, playerId: secondPlayerId);

        PlayerResearchOverview overview = await GetAsync();

        Assert.That(overview.Researchable.Select(technology => technology.Code), Is.EqualTo(new[] { "Mine" }));
    }

    // --- The surrounding state --------------------------------------------------------

    [Test, Description("The current project is named as well as identified, so a client can label it without holding the whole catalogue.")]
    public async Task ShouldNameTheCurrentProject()
    {
        await SeedCatalogueAsync([Technology("Chosen")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Chosen", TechnologyStatus.Unlocked, acquisitionPaidOnRound: 1);
        await SetCurrentProjectAsync("Chosen");

        PlayerResearchOverview overview = await GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(overview.CurrentTechnologyDisplayName, Is.EqualTo("Chosen"));
            Assert.That(overview.Researchable.Single().IsCurrentProject, Is.True);
        });
    }

    [Test, Description("No project selected comes back as null rather than an error, because it is a real state and the one the end-turn prompt exists for.")]
    public async Task ShouldReportNoCurrentProjectAsNull()
    {
        await SeedCatalogueAsync([Technology("Open")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Open", TechnologyStatus.Available);

        PlayerResearchOverview overview = await GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(overview.CurrentTechnologyId, Is.Null);
            Assert.That(overview.CurrentTechnologyDisplayName, Is.Null);
            Assert.That(overview.Researchable.Single().IsCurrentProject, Is.False);
        });
    }

    [Test, Description("The banked points and the Round are carried, since both are part of answering where the Player's research stands.")]
    public async Task ShouldCarryTheBankedPointsAndTheRound()
    {
        await SeedCatalogueAsync([Technology("Open")]);
        await SeedPlayerRowsAsync();
        await SetUnspentPointsAsync(directed: 120, diffusion: 7);
        await SetCurrentRoundAsync(4);

        PlayerResearchOverview overview = await GetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(overview.UnspentDirectedPoints, Is.EqualTo(120));
            Assert.That(overview.UnspentDiffusionPoints, Is.EqualTo(7));
            Assert.That(overview.CurrentRoundNumber, Is.EqualTo(4));
        });
    }

    [Test, Description("An acquisition cost is named as well as counted, because a client cannot resolve an Asset id without fetching the Asset catalogue.")]
    public async Task ShouldResolveAnAcquisitionCostToItsAssetName()
    {
        await SeedAssetAsync("Flint", "Flint");
        await SeedCatalogueAsync(
        [
            Technology("Costly") with { AcquisitionCosts = [Cost("Flint", 20)] }
        ]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Costly", TechnologyStatus.Available);

        ResearchableTechnology technology = (await GetAsync()).Researchable.Single();

        Assert.Multiple(() =>
        {
            Assert.That(technology.AcquisitionCosts.Single().AssetDisplayName, Is.EqualTo("Flint"));
            Assert.That(technology.AcquisitionCosts.Single().Quantity, Is.EqualTo(20));
        });
    }

    [Test, Description("A free technology comes back with an empty cost collection rather than null, so a client can count it without a null check.")]
    public async Task ShouldReturnAnEmptyCostCollectionForAFreeTechnology()
    {
        await SeedCatalogueAsync([Technology("Free")]);
        await SeedPlayerRowsAsync();
        await SetStatusAsync("Free", TechnologyStatus.Available);

        ResearchableTechnology technology = (await GetAsync()).Researchable.Single();

        Assert.Multiple(() =>
        {
            Assert.That(technology.AcquisitionCosts, Is.Empty);
            Assert.That(technology.ExperienceSources, Is.Empty);
        });
    }

    // --- Failure paths ----------------------------------------------------------------

    [Test, Description("A Player with no research state is reported as not found, because the row is written at setup and its absence is a broken game.")]
    public async Task ShouldReturnNotFoundWhenThePlayerHasNoResearchState()
    {
        int otherPlayerId = await AddPlayerAsync("Stateless");

        ServiceResult<PlayerResearchOverview> result = await _handler.Handle(
            new GetPlayerResearchRequest(GAME_ID, otherPlayerId),
            CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    // --- Helpers ----------------------------------------------------------------------

    private async Task<PlayerResearchOverview> GetAsync()
    {
        ServiceResult<PlayerResearchOverview> result = await _handler.Handle(
            new GetPlayerResearchRequest(GAME_ID, PlayerId),
            CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }
}
