using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Tests the technology step of game setup: every Player gets a row for every technology, nothing comes back Discovered, the catalogue's roots come back
/// Available and the rest Locked, and an unseeded catalogue fails setup loudly rather than producing a Player who can never research anything.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> generates the campaign map with raw SQL, as the sibling starter-asset fixture does. The technology
/// catalogue comes from <see cref="GameSqlServerTestBase.SeedTechnologyCatalogueAsync"/>, which runs the real seed data through the real upserts.
/// </remarks>
internal class StarterTechnologyTests : GameSqlServerTestBase
{
    private SetupGameRequest BuildRequest(int numberOfPlayers)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: "Starter Technology Game",
            NumberOfPlayers: numberOfPlayers,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    [Test, Description("Every Player gets a row for every technology, which is what materialised per-player requirements force and why presence means nothing.")]
    public async Task SetupWritesARowForEveryPlayerAndEveryTechnologyAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int[] playerIds = [.. await verify.Players.Where(p => p.GameId == result.SuccessValue).Select(p => p.Id).ToListAsync()];
        int technologyCount = await verify.Technologies.CountAsync();

        List<PlayerTechnology> rows = await verify.PlayerTechnologies
            .Where(row => playerIds.Contains(row.PlayerId))
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(technologyCount, Is.GreaterThan(0), "the catalogue has to be seeded for any of this to mean anything");
            Assert.That(rows, Has.Count.EqualTo(playerIds.Length * technologyCount));
            Assert.That(rows.All(row => row.GameId == result.SuccessValue), Is.True);
        }
    }

    [Test, Description("No technology comes back Discovered at setup, because the starter set is empty and a Player is meant to begin the game having researched nothing.")]
    public async Task ShouldDiscoverNothingAtSetupAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<PlayerTechnology> rows = await verify.PlayerTechnologies
            .Where(row => row.GameId == result.SuccessValue)
            .ToListAsync();

        // Resolved from the catalogue rather than from a restatement of the codes, so this follows StarterTechnologyCodes if it is ever filled again.
        HashSet<int> starterIds = [.. await verify.Technologies
            .Where(technology => GameSetupService.StarterTechnologyCodes.Contains(technology.Code))
            .Select(technology => technology.Id)
            .ToListAsync()];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(starterIds, Is.Empty, "the starter set is empty by design - this test wants rewriting if a code is put back");

            Assert.That(rows.Where(row => row.Status == TechnologyStatus.Discovered), Is.Empty);

            // No round stamp belongs on a row nobody has researched or paid for, which the table's own check constraint also insists on.
            Assert.That(rows.All(row => row.DiscoveredOnRound is null), Is.True);
            Assert.That(rows.All(row => row.AcquisitionPaidOnRound is null), Is.True);
        }
    }

    [Test, Description("Exactly the technologies with no prerequisite come out Available and every other one Locked, so the availability reconcile really ran as part of setup.")]
    public async Task ShouldLeaveOnlyThePrerequisiteFreeTechnologiesAvailableAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int playerId = await verify.Players.Where(p => p.GameId == result.SuccessValue).Select(p => p.Id).FirstAsync();

        // Derived from the catalogue's own edges, so this pins the reconcile's answer rather than carrying a second copy of which technologies are roots.
        HashSet<int> rootIds = [.. await verify.Technologies
            .Where(technology => !technology.Prerequisites.Any())
            .Select(technology => technology.Id)
            .ToListAsync()];

        List<PlayerTechnology> rows = await verify.PlayerTechnologies
            .Where(row => row.PlayerId == playerId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rootIds, Is.Not.Empty, "a catalogue with no roots would leave the Player nothing to research");

            Assert.That(
                rows.Where(row => row.Status == TechnologyStatus.Available).Select(row => row.TechnologyId),
                Is.EquivalentTo(rootIds));

            Assert.That(
                rows.Where(row => row.Status == TechnologyStatus.Locked).Select(row => row.TechnologyId),
                Is.EquivalentTo(rows.Select(row => row.TechnologyId).Except(rootIds)));

            // Nothing is Unlocked at setup: unlocking means paying the acquisition cost, which no Player has had a turn to do.
            Assert.That(rows.Where(row => row.Status == TechnologyStatus.Unlocked), Is.Empty);
        }
    }

    [Test, Description("Every Player gets a research state row, so no reader has to branch on its absence.")]
    public async Task SetupOpensAResearchStateForEveryPlayerAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 3);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 3), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<PlayerResearchState> states = await verify.PlayerResearchStates
            .Where(state => state.GameId == result.SuccessValue)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(states, Has.Count.EqualTo(3));
            Assert.That(states.All(state => state.CurrentTechnologyId is null), Is.True, "nothing is being researched until a Player chooses");
            Assert.That(states.All(state => state.UnspentDirectedPoints == 0), Is.True);
            Assert.That(states.All(state => state.TechPointsPerPopulationSize == 100), Is.True,
                "the opening divisor, written at setup rather than coalesced to on read, and positive so the first Turn's division cannot fault");

            Assert.That(states.All(state => state.LastResearchGeneratedOnRound is null), Is.True);
            Assert.That(states.All(state => state.LastResearchAppliedOnRound is null), Is.True);
        }
    }

    [Test, Description("A game is created with a configuration row holding the technology settings, so no reader ever coalesces to a hard-coded default.")]
    public async Task SetupCreatesTheGameConfigurationRowAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameConfiguration configuration = await verify.GameConfigurations.SingleAsync(c => c.GameId == result.SuccessValue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.TechnologyCustomisation, Is.EqualTo(TechnologyCustomisation.Identical),
                "the only implemented mode, and the only one a game may be created in");

            Assert.That(configuration.TechnologySpeedMultiplierPercent, Is.EqualTo(1_000), "100%, so the catalogue's figures come through unchanged");
            Assert.That(configuration.EmpireBaseResearchPoints, Is.GreaterThan(0));
        }
    }

    [Test, Description("Under Identical customisation every Player faces the same requirements in all three buckets, which is the correct answer for the mode rather than a stand-in.")]
    public async Task SetupGivesEveryPlayerTheSameRequirementsUnderIdenticalAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        // Grouped in memory rather than in the query, because a bucket a technology does not use is null and SQL's
        // COUNT(DISTINCT) skips nulls - so a technology off directed research entirely, of which the catalogue has
        // several, would come back with zero distinct values and read as disagreement rather than as agreement on
        // null. The game's rows number a few hundred, so reading them to compare them costs nothing.
        List<PlayerTechnology> rows = await verify.PlayerTechnologies
            .Where(row => row.GameId == result.SuccessValue)
            .ToListAsync();

        Assert.That(rows, Is.Not.Empty, "nothing below says anything about an empty set");

        // All three buckets, not just the directed one: the generator writes each from the same catalogue figure and
        // the same multiplier, so any of them differing between Players would mean the mode was not identical.
        List<IGrouping<int, PlayerTechnology>> byTechnology = [.. rows.GroupBy(row => row.TechnologyId)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byTechnology.All(group => group.Select(row => row.DirectedRequired).Distinct().Count() == 1), Is.True,
                "every Player's directed requirement for a technology, the unused null included, is the same figure");

            Assert.That(byTechnology.All(group => group.Select(row => row.ExperienceRequired).Distinct().Count() == 1), Is.True,
                "and so is the experience requirement");

            Assert.That(byTechnology.All(group => group.Select(row => row.DiffusionRequired).Distinct().Count() == 1), Is.True,
                "and the diffusion requirement");
        }
    }

    [Test, Description("Setup fails loudly when the technology catalogue is empty, rather than starting a game whose Players can never research anything.")]
    public async Task SetupFailsWhenTheTechnologyCatalogueIsEmptyAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        // The base seeds the catalogue for every fixture, so emptying it is how this case is reached at all.
        await using (AscendantEmpireContext purge = await DbFactory.CreateDbContextAsync())
        {
            await purge.TechnologyExperienceTypes.ExecuteDeleteAsync();
            await purge.TechnologyPrerequisites.ExecuteDeleteAsync();
            await purge.TechnologyAcquisitionCosts.ExecuteDeleteAsync();
            await purge.TechnologyGrants.ExecuteDeleteAsync();
            await purge.Technologies.ExecuteDeleteAsync();
        }

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("Technologies database seed"),
                "the message has to say what to do about it, since an unseeded catalogue is the likely cause");
        }
    }
}
