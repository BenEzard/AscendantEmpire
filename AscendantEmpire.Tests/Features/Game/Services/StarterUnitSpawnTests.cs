using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Game.Services;

/// <summary>
/// Integration tests for the starter-unit step of game setup: every player gets the starter templates
/// unlocked and one settler (at its tag's default size) plus one scout spawned at their start position,
/// and a misconfigured catalogue or map fails setup loudly.
/// </summary>
/// <remarks>
/// Runs at the SqlServer tier because <c>SetupGameAsync</c> generates the campaign map with raw SQL.
/// The starter catalogue and start positions are seeded by <see cref="GameSqlServerTestBase.SeedGameAsync"/>.
/// </remarks>
internal class StarterUnitSpawnTests : GameSqlServerTestBase
{
    private SetupGameRequest BuildRequest(int numberOfPlayers)
    {
        return new SetupGameRequest(
            GalleryMapId: _galleryIndexId,
            GameName: "Starter Units Game",
            NumberOfPlayers: numberOfPlayers,
            BattleMapSize: 50,
            GenerationSeed: null);
    }

    [Test, Description("Game setup spawns a settler (tag default size) and a scout (1) for every player at their start position, all healthy.")]
    public async Task SetupSpawnsStarterUnitsForEveryPlayerAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        int gameId = result.SuccessValue;

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<Player> players = await verify.Players.Where(p => p.GameId == gameId).ToListAsync();
        List<UnitInstance> troops = await verify.UnitInstances.Where(t => t.GameId == gameId).ToListAsync();

        // The seeded catalogue's template codes, resolved to ids for the assertions below.
        int settlerTemplateId = (await verify.UnitTemplates.SingleAsync(t => t.Code == "SETTLER")).Id;
        int scoutTemplateId = (await verify.UnitTemplates.SingleAsync(t => t.Code == "SCOUT")).Id;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(troops, Has.Count.EqualTo(players.Count * 2), "One settler + one scout per player.");

            foreach (Player player in players)
            {
                List<UnitInstance> playerTroops = [.. troops.Where(t => t.PlayerId == player.Id)];

                Assert.That(playerTroops, Has.Count.EqualTo(2), $"Player {player.Id} has a settler and a scout.");

                UnitInstance settler = playerTroops.Single(t => t.UnitTemplateId == settlerTemplateId);
                UnitInstance scout = playerTroops.Single(t => t.UnitTemplateId == scoutTemplateId);

                // Settler spawns at its unled tag's default size (200), the scout as a single individual.
                Assert.That(settler.InitialStrength, Is.EqualTo(200));
                Assert.That(settler.HealthyCount, Is.EqualTo(200));
                Assert.That(settler.InjuredCount, Is.Zero);
                Assert.That(scout.InitialStrength, Is.EqualTo(1));
                Assert.That(scout.HealthyCount, Is.EqualTo(1));

                // Both units share the player's single start tile.
                Assert.That(scout.CampaignMapTileId, Is.EqualTo(settler.CampaignMapTileId), "Both starting units spawn on the player's start tile.");
            }

            // Each player got their own start position.
            int[] distinctTiles = [.. troops.Select(t => t.CampaignMapTileId).Distinct()];
            Assert.That(distinctTiles, Has.Length.EqualTo(players.Count), "Players start on distinct tiles.");
        }
    }

    [Test, Description("Game setup seeds the starter template and component unlocks for every player.")]
    public async Task SetupSeedsStarterUnlocksForEveryPlayerAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        int gameId = result.SuccessValue;

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<int> playerIds = await verify.Players.Where(p => p.GameId == gameId).Select(p => p.Id).ToListAsync();
        List<PlayerUnlockedTemplate> templateUnlocks = await verify.PlayerUnlockedTemplates.Where(u => playerIds.Contains(u.PlayerId)).ToListAsync();
        List<PlayerUnlockedComponent> componentUnlocks = await verify.PlayerUnlockedComponents.Where(u => playerIds.Contains(u.PlayerId)).ToListAsync();

        // The seeded starter templates share one body; only the templates' own components are unlocked.
        int bodyComponentId = (await verify.UnitComponents.SingleAsync(c => c.Code == "human-body")).Id;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(templateUnlocks, Has.Count.EqualTo(playerIds.Count * 2), "Both starter templates unlocked per player.");

            foreach (int playerId in playerIds)
            {
                Assert.That(componentUnlocks.Where(u => u.PlayerId == playerId).Select(u => u.UnitComponentId),
                    Does.Contain(bodyComponentId), $"Player {playerId} has the starter body unlocked.");
            }
        }
    }

    [Test, Description("Setup fails loudly when the map has fewer start positions than the game has players.")]
    public async Task SetupFailsWithInsufficientStartPositionsAsync()
    {
        // Two start positions are seeded, but three players are requested.
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 3), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("start position"));
        }
    }

    [Test, Description("Setup fails loudly when a starter template has no compiled snapshot.")]
    public async Task SetupFailsWithoutCompiledStarterSnapshotsAsync()
    {
        await SeedGameAsync(mapWidth: 2, mapHeight: 2, numberOfPlayers: 2);

        // Remove the compiled snapshots after seeding — the catalogue is now half-configured.
        await using (AscendantEmpireContext sabotage = await DbFactory.CreateDbContextAsync())
        {
            List<CompiledUnitTemplate> snapshots = await sabotage.CompiledUnitTemplates.ToListAsync();
            sabotage.CompiledUnitTemplates.RemoveRange(snapshots);
            await sabotage.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(BuildRequest(numberOfPlayers: 2), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("no compiled snapshot"));
        }
    }
}
