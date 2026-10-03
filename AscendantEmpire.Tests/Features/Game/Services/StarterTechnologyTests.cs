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
/// Tests the technology step of game setup: every Player gets a row for every technology, the starters come back Discovered, their dependents become
/// Available, and an unseeded catalogue fails setup loudly rather than producing a Player who can never research anything.
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

        Assert.Multiple(() =>
        {
            Assert.That(technologyCount, Is.GreaterThan(0), "the catalogue has to be seeded for any of this to mean anything");
            Assert.That(rows, Has.Count.EqualTo(playerIds.Length * technologyCount));
            Assert.That(rows.All(row => row.GameId == result.SuccessValue), Is.True);
        });
    }

    [Test, Description("Each Player gets exactly the starter technologies Discovered on round 0, resolved from the catalogue rather than a second copy of the codes.")]
    public async Task SetupDiscoversExactlyTheStarterTechnologiesAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int[] playerIds = [.. await verify.Players.Where(p => p.GameId == result.SuccessValue).Select(p => p.Id).ToListAsync()];

        // Resolved from the catalogue, so this pins the setup service against the codes it asks for rather than against a restatement of them.
        HashSet<int> expectedStarterIds = [.. await verify.Technologies
            .Where(technology => GameSetupService.StarterTechnologyCodes.Contains(technology.Code))
            .Select(technology => technology.Id)
            .ToListAsync()];

        foreach (int playerId in playerIds)
        {
            List<PlayerTechnology> discovered = await verify.PlayerTechnologies
                .Where(row => row.PlayerId == playerId && row.Status == TechnologyStatus.Discovered)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(expectedStarterIds, Is.Not.Empty);
                Assert.That(discovered.Select(row => row.TechnologyId), Is.EquivalentTo(expectedStarterIds));
                Assert.That(discovered.All(row => row.DiscoveredOnRound == 0), Is.True, "granted at setup, before the first Round opened");

                // Never paid for, so no acquisition round is stamped — and the buckets are filled to their requirement so the totals agree with the status.
                Assert.That(discovered.All(row => row.AcquisitionPaidOnRound is null), Is.True);
                Assert.That(discovered.All(row => row.DirectedPoints == (row.DirectedRequired ?? 0)), Is.True);
            });
        }
    }

    [Test, Description("The starters' dependents come out Available and the rest Locked, so the availability reconcile really ran as part of setup.")]
    public async Task SetupLeavesTheStartersDependentsAvailableAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        int playerId = await verify.Players.Where(p => p.GameId == result.SuccessValue).Select(p => p.Id).FirstAsync();

        List<PlayerTechnology> rows = await verify.PlayerTechnologies
            .Where(row => row.PlayerId == playerId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rows.Any(row => row.Status == TechnologyStatus.Available), Is.True,
                "the seeded tree has technologies with no prerequisites besides the starters, so some must be available");

            Assert.That(rows.Any(row => row.Status == TechnologyStatus.Locked), Is.True,
                "and most of the tree sits behind the starters");

            // Nothing is Unlocked at setup: unlocking means paying the acquisition cost, which no Player has had a turn to do.
            Assert.That(rows.Any(row => row.Status == TechnologyStatus.Unlocked), Is.False);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(states, Has.Count.EqualTo(3));
            Assert.That(states.All(state => state.CurrentTechnologyId is null), Is.True, "nothing is being researched until a Player chooses");
            Assert.That(states.All(state => state.UnspentDirectedPoints == 0), Is.True);
            Assert.That(states.All(state => state.LastResearchAppliedOnRound is null), Is.True);
        });
    }

    [Test, Description("A game is created with a configuration row holding the technology settings, so no reader ever coalesces to a hard-coded default.")]
    public async Task SetupCreatesTheGameConfigurationRowAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        GameConfiguration configuration = await verify.GameConfigurations.SingleAsync(c => c.GameId == result.SuccessValue);

        Assert.Multiple(() =>
        {
            Assert.That(configuration.TechnologyCustomisation, Is.EqualTo(TechnologyCustomisation.Identical),
                "the only implemented mode, and the only one a game may be created in");

            Assert.That(configuration.TechnologySpeedMultiplierPercent, Is.EqualTo(1_000), "100%, so the catalogue's figures come through unchanged");
            Assert.That(configuration.EmpireBaseResearchPoints, Is.GreaterThan(0));
        });
    }

    [Test, Description("Under Identical customisation every Player faces the same requirements, which is the correct answer for the mode rather than a stand-in.")]
    public async Task SetupGivesEveryPlayerTheSameRequirementsUnderIdenticalAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        var requirementsByTechnology = await verify.PlayerTechnologies
            .Where(row => row.GameId == result.SuccessValue)
            .GroupBy(row => row.TechnologyId)
            .Select(group => new
            {
                TechnologyId = group.Key,
                DistinctDirected = group.Select(row => row.DirectedRequired).Distinct().Count()
            })
            .ToListAsync();

        Assert.That(requirementsByTechnology.All(entry => entry.DistinctDirected == 1), Is.True);
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

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(string.Join("; ", result.Errors), Does.Contain("Technologies database seed"),
                "the message has to say what to do about it, since an unseeded catalogue is the likely cause");
        });
    }
}
