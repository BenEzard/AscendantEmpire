using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Tests the asset-unlock step of game setup: every Player starts with the same starter set of Assets and meta-categories, and an unseeded catalogue fails
/// setup loudly rather than producing a Player with nothing.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> generates the campaign map with raw SQL, as the sibling starter-unit fixture does. The asset
/// catalogue comes from <see cref="GameSqlServerTestBase.SeedStarterAssetsAsync"/>, which runs the real seed data through the real upsert.
/// </remarks>
internal class StarterAssetUnlockTests : GameSqlServerTestBase
{
    private SetupGameRequest BuildRequest(int numberOfPlayers)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: "Starter Assets Game",
            NumberOfPlayers: numberOfPlayers,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    [Test, Description("Every Player is granted exactly the starter Assets and meta-categories, on round 0, so all three toolbar icons are present from the start.")]
    public async Task SetupUnlocksTheStarterAssetsForEveryPlayerAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<Player> players = await verify.Players.Where(p => p.GameId == result.SuccessValue).ToListAsync();
        int[] playerIds = [.. players.Select(p => p.Id)];

        // Resolved from the catalogue rather than hard-coded, so this pins the setup service against the codes it asks for, not against a second copy of them.
        HashSet<int> expectedAssetIds = [.. await verify.Assets
            .Where(r => GameSetupService.StarterAssetCodes.Contains(r.Code))
            .Select(r => r.Id)
            .ToListAsync()];

        HashSet<int> expectedMetaCategoryIds = [.. await verify.AssetMetaCategories
            .Where(m => GameSetupService.StarterAssetMetaCategoryCodes.Contains(m.Code))
            .Select(m => m.Id)
            .ToListAsync()];

        List<PlayerUnlockedAsset> assetUnlocks = await verify.PlayerUnlockedAssets
            .Where(u => playerIds.Contains(u.PlayerId))
            .ToListAsync();

        List<PlayerUnlockedAssetMetaCategory> metaCategoryUnlocks = await verify.PlayerUnlockedAssetMetaCategories
            .Where(u => playerIds.Contains(u.PlayerId))
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(expectedAssetIds, Has.Count.EqualTo(GameSetupService.StarterAssetCodes.Length), "The seeded catalogue does not carry every starter asset code.");

            foreach (int playerId in playerIds)
            {
                Assert.That(
                    assetUnlocks.Where(u => u.PlayerId == playerId).Select(u => u.AssetId),
                    Is.EquivalentTo(expectedAssetIds),
                    $"Player {playerId} did not get exactly the starter assets.");

                Assert.That(
                    metaCategoryUnlocks.Where(u => u.PlayerId == playerId).Select(u => u.AssetMetaCategoryId),
                    Is.EquivalentTo(expectedMetaCategoryIds),
                    $"Player {playerId} did not get exactly the starter meta-categories.");
            }

            Assert.That(assetUnlocks.Select(u => u.UnlockedOnRound), Is.All.Zero, "Starter unlocks are granted at setup, before the first round.");
            Assert.That(metaCategoryUnlocks.Select(u => u.UnlockedOnRound), Is.All.Zero);
        });
    }

    [Test, Description("Nothing outside the starter set is unlocked, so the assets held back for Technology stay invisible.")]
    public async Task SetupLeavesNonStarterAssetsLockedAsync()
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

        List<int> heldBackIds = await verify.Assets
            .Where(r => heldBackCodes.Contains(r.Code))
            .Select(r => r.Id)
            .ToListAsync();

        bool anyHeldBackUnlocked = await verify.PlayerUnlockedAssets.AnyAsync(u => heldBackIds.Contains(u.AssetId));

        Assert.Multiple(() =>
        {
            Assert.That(heldBackIds, Has.Count.EqualTo(heldBackCodes.Length), "The seeded catalogue does not carry every held-back code this test names.");
            Assert.That(anyHeldBackUnlocked, Is.False, "An asset meant to wait for Technology was unlocked at setup.");
        });
    }

    [Test, Description("Starter unlocks carry no obsolescence, so nothing a Player begins with is already counting down.")]
    public async Task SetupGrantsStarterAssetsWithNoObsolescenceAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 1);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 1), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await verify.PlayerUnlockedAssets.AnyAsync(u => u.ObsoleteOnRound != null), Is.False);
            Assert.That(await verify.PlayerUnlockedAssetMetaCategories.AnyAsync(u => u.ObsoleteOnRound != null), Is.False);
        });
    }

    [Test, Description("An unseeded asset catalogue fails setup with the missing codes named, rather than starting a Player with an empty asset panel.")]
    public async Task SetupFailsWhenTheAssetCatalogueIsMissingAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 1);

        // Undo the base class's asset seed, leaving everything else game setup needs in place, so the failure can only be about assets.
        await using (AscendantEmpireContext strip = await DbFactory.CreateDbContextAsync())
        {
            await strip.Assets.ExecuteDeleteAsync();
            await strip.AssetGroups.ExecuteDeleteAsync();
            await strip.AssetCategories.ExecuteDeleteAsync();
            await strip.AssetMetaCategories.ExecuteDeleteAsync();
        }

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False, "Setup succeeded against an empty asset catalogue.");
            Assert.That(string.Join("; ", result.Errors), Does.Contain("Starter asset(s) not found").And.Contain("ConstructionMaterials"));
        });
    }
}
