using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests that discovering "Biome Skill - Arid" raises the Player's Hunt and Forage on Arid tiles, end to end from the authored seed data.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier, and deliberately whole: the real technology seed data is upserted, the real reconcile runs, and the real reader is asked for the
/// answer. A test that hand-built the grant rows would pass with no grant authored at all.
/// </para>
/// <para>
/// Arid's Hunt and Forage bases are 30 whole units each, so a twenty percent bonus is a clean 36. Desert carries no Hunt base, which is what pins the rule
/// that no modifier gives a resource to a biome that does not have it.
/// </para>
/// <para>
/// <b>None of this raises a Settlement's harvest.</b> <c>SettlementHarvestCalculator.CalculateSustenanceTake</c> takes the least of available, regrowth and the
/// staffed share, and regrowth is measured against the biome's base - so a positive modifier raises only the term that is never the smallest. It shows up for
/// a Force feeding on Arid ground, and nowhere else, until an owned tile's regrowth uses its owner's effective value.
/// </para>
/// </remarks>
[TestFixture]
internal class BiomeSkillAridGrantTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const string _BIOME_SKILL_ARID = "BiomeSkillArid";
    private const string _SECOND_GRANTING_TECHNOLOGY = "BiomeExpertiseArid";
    private const string _ALL_BIOMES_TECHNOLOGY = "Acclimitisation";

    private readonly PlayerSustenanceValueReader _reader = new();

    private readonly PlayerTechnologySustenanceModifierService _service =
        new(NullLogger<PlayerTechnologySustenanceModifierService>.Instance);

    private int _playerId;

    [SetUp]
    public async Task SeedCatalogueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Biomes first: the technology upsert rejects a grant naming a biome with no base value for the resource.
        context.CampaignBiomes.AddRange(
            BuildBiome(BiomeType.Arid, "Arid", forage: 30, hunt: 30),
            BuildBiome(BiomeType.Grassland, "Grassland", forage: 40, hunt: 20, fodder: 50),
            BuildBiome(BiomeType.Desert, "Desert", forage: 10, hunt: null));

        Player player = BuildPlayer();
        context.Players.Add(player);

        await context.SaveChangesAsync();

        _playerId = player.Id;

        await UpsertAsync(new UpsertExperienceTypesHandler(NullLogger<UpsertExperienceTypesHandler>.Instance, context)
            .Handle(new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build()), CancellationToken.None));

        await TechnologySeedTestAssets.SeedMaterialAssetsAsync(context);

        await UpsertAsync(new UpsertTechnologiesHandler(NullLogger<UpsertTechnologiesHandler>.Instance, context)
            .Handle(new UpsertTechnologiesRequest(TechnologySeedData.Build()), CancellationToken.None));
    }

    [Test, Description("Discovering Biome Skill - Arid raises the Player's Hunt and Forage on Arid by a fifth, which is what the Technology is for.")]
    public async Task ShouldRaiseHuntAndForageOnAridWhenBiomeSkillAridIsDiscovered()
    {
        await DiscoverAsync(_BIOME_SKILL_ARID);

        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(36)));
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(36)));
        }
    }

    [Test, Description("The grants name Arid, so no other biome moves - Biome Skill - Desert is a separate Technology with its own grants.")]
    public async Task ShouldLeaveOtherBiomesAloneWhenBiomeSkillAridIsDiscovered()
    {
        await DiscoverAsync(_BIOME_SKILL_ARID);

        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(20)));
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(40)));
            Assert.That(values.GetValue(BiomeType.Desert, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(10)));
        }
    }

    [Test, Description("Only the two granted resources move, so Arid's Fish and Fodder are untouched by a Hunt and Forage bonus.")]
    public async Task ShouldLeaveFishAndFodderAloneWhenBiomeSkillAridIsDiscovered()
    {
        await DiscoverAsync(_BIOME_SKILL_ARID);

        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Fish), Is.Null);
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Fodder), Is.Null);
        }
    }

    [Test, Description("A Player who has not discovered it gets Arid's base values, since the modifier rows are what the bonus is made of.")]
    public async Task ShouldGiveNothingWhenBiomeSkillAridIsNotDiscovered()
    {
        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(30)));
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(30)));
        }
    }

    [Test, Description("An all-biomes Hunt bonus leaves a biome with no Hunt base null rather than zero, because no modifier gives a resource a biome lacks.")]
    public async Task ShouldBeInertWhereBiomeHasNoBase()
    {
        await AddAllBiomesHuntGrantAsync(_ALL_BIOMES_TECHNOLOGY);
        await DiscoverAsync(_ALL_BIOMES_TECHNOLOGY);

        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Desert, SustenanceResource.Hunt), Is.Null);
            Assert.That(values.GetValue(BiomeType.Arid, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(36)));
        }
    }

    [Test, Description("Two technologies granting the same resource and biome sum their percentages rather than compounding, so two fifths of 30 is 42.")]
    public async Task ShouldSumBothWhenTwoTechnologiesGrantTheSameResourceAndBiome()
    {
        await AddAridHuntGrantAsync(_SECOND_GRANTING_TECHNOLOGY);
        await DiscoverAsync(_BIOME_SKILL_ARID);
        await DiscoverAsync(_SECOND_GRANTING_TECHNOLOGY);

        PlayerSustenanceValues values = await ReconcileAndReadAsync();

        Assert.That(
            values.GetValue(BiomeType.Arid, SustenanceResource.Hunt),
            Is.EqualTo(ScaledAmount.FromWhole(42)),
            "compounding would give 43.2");
    }

    #region Helpers

    private static Player BuildPlayer()
    {
        return new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Tester",
            FactionName = "Testers",
            PeopleName = "Test People",
            Demonym = "Testian",
            Language = "Testish",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active
        };
    }

    /// <summary>
    /// A biome with the given sustenance values, in whole numbers, and no Fish.
    /// </summary>
    private static CampaignBiome BuildBiome(BiomeType biomeType, string name, int? forage, int? hunt, int? fodder = null)
    {
        return new CampaignBiome
        {
            BiomeTypeId = biomeType,
            Name = name,
            MovementCost = 1m,
            IsTraversable = true,
            MapEditorHtmlColor = "#fcfda4",
            RegenerationPercent = 0.15m,
            BaseForageValue = forage is null ? null : ScaledAmount.FromWhole(forage.Value).RawValue,
            BaseHuntValue = hunt is null ? null : ScaledAmount.FromWhole(hunt.Value).RawValue,
            BaseFishValue = null,
            BaseFodderValue = fodder is null ? null : ScaledAmount.FromWhole(fodder.Value).RawValue
        };
    }

    private static async Task UpsertAsync(Task<ServiceResultBase> seeding)
    {
        ServiceResultBase result = await seeding;

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>
    /// Marks one seeded Technology as Discovered for the Player, which is the only state the reconcile reads.
    /// </summary>
    private async Task DiscoverAsync(string technologyCode)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int technologyId = await TechnologyIdAsync(context, technologyCode);

        context.PlayerTechnologies.Add(new PlayerTechnology
        {
            PlayerId = _playerId,
            TechnologyId = technologyId,
            GameId = _GAME_ID,
            Status = TechnologyStatus.Discovered,
            DirectedRequired = 100,
            DirectedPoints = 0,
            ExperiencePoints = 0,
            DiffusionPoints = 0
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Gives a second Technology the same +20% Hunt on Arid, so the summing rule can be seen through the reader.
    /// </summary>
    private async Task AddAridHuntGrantAsync(string technologyCode)
    {
        await AddGrantAsync(technologyCode, BiomeType.Arid);
    }

    /// <summary>
    /// Gives a Technology a +20% Hunt bonus in every biome, which is the shape that meets a biome with no Hunt base.
    /// </summary>
    private async Task AddAllBiomesHuntGrantAsync(string technologyCode)
    {
        await AddGrantAsync(technologyCode, biome: null);
    }

    private async Task AddGrantAsync(string technologyCode, BiomeType? biome)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(new TechnologySustenanceModifierGrant
        {
            TechnologyId = await TechnologyIdAsync(context, technologyCode),
            Resource = SustenanceResource.Hunt,
            BiomeTypeId = biome,
            FlatValue = 0,
            PercentValue = ScaledPercent.FromWholePercent(20).RawValue
        });

        await context.SaveChangesAsync();
    }

    private static async Task<int> TechnologyIdAsync(AscendantEmpireContext context, string technologyCode)
    {
        return await context.Technologies
            .Where(technology => technology.Code == technologyCode)
            .Select(technology => technology.Id)
            .SingleAsync();
    }

    /// <summary>
    /// Runs the reconcile, commits it the way a real caller does, and reads the Player's effective values back.
    /// </summary>
    private async Task<PlayerSustenanceValues> ReconcileAndReadAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ReconcileAsync(context, _GAME_ID, _playerId, CancellationToken.None);

        await context.SaveChangesAsync();

        return await _reader.GetValuesAsync(context, _playerId);
    }

    #endregion
}
