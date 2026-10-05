using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests that the database enforces the two rules on <c>game.PlayerSustenanceModifier</c>: one row per source, resource and biome, and no row that changes nothing.
/// </summary>
/// <remarks>
/// <para>
/// Nothing writes modifiers yet, so these constraints are the only thing enforcing either rule - and the unique index relies on <c>HasFilter(null)</c>, which EF's
/// SQL Server convention would otherwise quietly replace with an <c>IS NOT NULL</c> filter that lets all-biome rows duplicate.
/// </para>
/// <para>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is
/// restored from the migrated template, so this also proves the migration created them.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerSustenanceModifierConstraintTests : SqlServerTestBase
{
    private const int _SOURCE_ID = 10;

    private int _gameId;
    private int _playerId;

    [SetUp]
    public async Task SeedPlayerAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Biomes are seeded by the Entity Setup Tool rather than by a migration, so the template database may not have the one a biome-specific row names.
        if (!await context.CampaignBiomes.AnyAsync(b => b.BiomeTypeId == BiomeType.Grassland))
        {
            context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Grassland, Name = "Grassland", MovementCost = 1m, IsTraversable = true, MapEditorHtmlColor = "#7cb342" });
        }

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = "Modifier Game",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var player = new Player
        {
            GameId = game.Id,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Ruler",
            FactionName = "Faction",
            PeopleName = "People",
            Demonym = "Person",
            CultureName = "Culture",
            Language = "Language",
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        };

        context.Players.Add(player);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
    }

    [Test, Description("A source cannot hold two all-biome rows for the same resource, because NULL biomes must collide in the unique index rather than be filtered out of it.")]
    public async Task ShouldRejectASecondAllBiomesModifierFromTheSameSourceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.Add(BuildModifier(biome: null, flatValue: 1_000, percentValue: 0));
        await context.SaveChangesAsync();

        context.PlayerSustenanceModifiers.Add(BuildModifier(biome: null, flatValue: 2_000, percentValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("The same source may hold an all-biome row and a biome-specific row for one resource, since those modify different sets of values.")]
    public async Task ShouldAllowAnAllBiomesAndABiomeSpecificModifierFromTheSameSourceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.AddRange(
            BuildModifier(biome: null, flatValue: 1_000, percentValue: 0),
            BuildModifier(BiomeType.Grassland, flatValue: 0, percentValue: 100));

        await context.SaveChangesAsync();

        Assert.That(await context.PlayerSustenanceModifiers.CountAsync(m => m.PlayerId == _playerId), Is.EqualTo(2));
    }

    [Test, Description("A row with neither a flat nor a percentage part is rejected, because it would change nothing while still using up the source's one row.")]
    public async Task ShouldRejectAModifierWithNeitherAFlatNorAPercentagePartAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.Add(BuildModifier(biome: null, flatValue: 0, percentValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    /// <summary>
    /// A Forage modifier from the fixture's one Technology source, with raw scaled amounts.
    /// </summary>
    private PlayerSustenanceModifier BuildModifier(BiomeType? biome, long flatValue, long percentValue)
    {
        return new PlayerSustenanceModifier
        {
            GameId = _gameId,
            PlayerId = _playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = _SOURCE_ID,
            Resource = SustenanceResource.Forage,
            BiomeTypeId = biome,
            FlatValue = flatValue,
            PercentValue = percentValue
        };
    }
}
