using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Resources;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Tests the resource-unlock step of game setup: every Player starts with the same starter set of Resources and meta-categories, and an unseeded catalogue fails
/// setup loudly rather than producing a Player with nothing.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> generates the campaign map with raw SQL, as the sibling starter-unit fixture does. The resource
/// catalogue comes from <see cref="GameSqlServerTestBase.SeedStarterResourcesAsync"/>, which runs the real seed data through the real upsert.
/// </remarks>
internal class StarterResourceUnlockTests : GameSqlServerTestBase
{
    private SetupGameRequest BuildRequest(int numberOfPlayers)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: "Starter Resources Game",
            NumberOfPlayers: numberOfPlayers,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    [Test, Description("Every Player is granted exactly the starter Resources and meta-categories, on round 0, so all three toolbar icons are present from the start.")]
    public async Task SetupUnlocksTheStarterResourcesForEveryPlayerAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<Player> players = await verify.Players.Where(p => p.GameId == result.SuccessValue).ToListAsync();
        int[] playerIds = [.. players.Select(p => p.Id)];

        // Resolved from the catalogue rather than hard-coded, so this pins the setup service against the codes it asks for, not against a second copy of them.
        HashSet<int> expectedResourceIds = [.. await verify.Resources
            .Where(r => GameSetupService.StarterResourceCodes.Contains(r.Code))
            .Select(r => r.Id)
            .ToListAsync()];

        HashSet<int> expectedMetaCategoryIds = [.. await verify.ResourceMetaCategories
            .Where(m => GameSetupService.StarterResourceMetaCategoryCodes.Contains(m.Code))
            .Select(m => m.Id)
            .ToListAsync()];

        List<PlayerUnlockedResource> resourceUnlocks = await verify.PlayerUnlockedResources
            .Where(u => playerIds.Contains(u.PlayerId))
            .ToListAsync();

        List<PlayerUnlockedResourceMetaCategory> metaCategoryUnlocks = await verify.PlayerUnlockedResourceMetaCategories
            .Where(u => playerIds.Contains(u.PlayerId))
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(expectedResourceIds, Has.Count.EqualTo(GameSetupService.StarterResourceCodes.Length), "The seeded catalogue does not carry every starter resource code.");

            foreach (int playerId in playerIds)
            {
                Assert.That(
                    resourceUnlocks.Where(u => u.PlayerId == playerId).Select(u => u.ResourceId),
                    Is.EquivalentTo(expectedResourceIds),
                    $"Player {playerId} did not get exactly the starter resources.");

                Assert.That(
                    metaCategoryUnlocks.Where(u => u.PlayerId == playerId).Select(u => u.ResourceMetaCategoryId),
                    Is.EquivalentTo(expectedMetaCategoryIds),
                    $"Player {playerId} did not get exactly the starter meta-categories.");
            }

            Assert.That(resourceUnlocks.Select(u => u.UnlockedOnRound), Is.All.Zero, "Starter unlocks are granted at setup, before the first round.");
            Assert.That(metaCategoryUnlocks.Select(u => u.UnlockedOnRound), Is.All.Zero);
        });
    }

    [Test, Description("Nothing outside the starter set is unlocked, so the resources held back for Technology stay invisible.")]
    public async Task SetupLeavesNonStarterResourcesLockedAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 1);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 1), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        // A named timber, a plank, a prestige stone, a higher grade and a processed material - one from each category of thing deliberately held back.
        string[] heldBackCodes =
        [
            "ConstructionMaterials.Wood.Logs.OakLog",
            "ConstructionMaterials.Wood.Planks.OakPlank",
            "ConstructionMaterials.Stone.RawRock.Marble",
            "Weapons.MeleeWeapons.WoodClub.High",
            "RawMaterials.RawMaterials.Leather"
        ];

        List<int> heldBackIds = await verify.Resources
            .Where(r => heldBackCodes.Contains(r.Code))
            .Select(r => r.Id)
            .ToListAsync();

        bool anyHeldBackUnlocked = await verify.PlayerUnlockedResources.AnyAsync(u => heldBackIds.Contains(u.ResourceId));

        Assert.Multiple(() =>
        {
            Assert.That(heldBackIds, Has.Count.EqualTo(heldBackCodes.Length), "The seeded catalogue does not carry every held-back code this test names.");
            Assert.That(anyHeldBackUnlocked, Is.False, "A resource meant to wait for Technology was unlocked at setup.");
        });
    }

    [Test, Description("Starter unlocks carry no obsolescence, so nothing a Player begins with is already counting down.")]
    public async Task SetupGrantsStarterResourcesWithNoObsolescenceAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 1);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 1), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await verify.PlayerUnlockedResources.AnyAsync(u => u.ObsoleteOnRound != null), Is.False);
            Assert.That(await verify.PlayerUnlockedResourceMetaCategories.AnyAsync(u => u.ObsoleteOnRound != null), Is.False);
        });
    }

    [Test, Description("An unseeded resource catalogue fails setup with the missing codes named, rather than starting a Player with an empty resource panel.")]
    public async Task SetupFailsWhenTheResourceCatalogueIsMissingAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 1);

        // Undo the base class's resource seed, leaving everything else game setup needs in place, so the failure can only be about resources.
        await using (AscendantEmpireContext strip = await DbFactory.CreateDbContextAsync())
        {
            await strip.Resources.ExecuteDeleteAsync();
            await strip.ResourceGroups.ExecuteDeleteAsync();
            await strip.ResourceCategories.ExecuteDeleteAsync();
            await strip.ResourceMetaCategories.ExecuteDeleteAsync();
        }

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False, "Setup succeeded against an empty resource catalogue.");
            Assert.That(string.Join("; ", result.Errors), Does.Contain("Starter resource(s) not found").And.Contain("ConstructionMaterials"));
        });
    }
}
