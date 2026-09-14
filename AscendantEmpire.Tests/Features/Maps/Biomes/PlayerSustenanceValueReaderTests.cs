using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="PlayerSustenanceValueReader"/>: that it applies the right modifiers to the right biome values for the right Player.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The reader uses only LINQ reads, with no raw SQL, bulk updates or transactions. The formula itself, floors included, is covered without a
/// database in <c>SustenanceValueCalculatorTests</c>.
/// </para>
/// <para>
/// The catalogue has two biomes: Grassland (Forage 400, Hunt 100, no Fish, Fodder 1,000) and Forest (Forage 300, Hunt 150, no Fish, Fodder 100). Player 2 is the Player
/// being read about; Player 3 is anyone else. Nothing checks the Players exist, so no Game or Player rows are seeded.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerSustenanceValueReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;

    /// <summary>Distinct source ids, so no two seeded modifiers could ever share a unique key.</summary>
    private const int _FIRST_SOURCE_ID = 10;
    private const int _SECOND_SOURCE_ID = 11;

    private readonly PlayerSustenanceValueReader _reader = new();

    [SetUp]
    public async Task SeedBiomesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignBiomes.AddRange(
            BuildBiome(BiomeType.Grassland, "Grassland", forage: 400, hunt: 100, fodder: 1_000),
            BuildBiome(BiomeType.Forest, "Forest", forage: 300, hunt: 150, fodder: 100));

        await context.SaveChangesAsync();
    }

    [Test, Description("A Player holding no modifiers gets every biome's base values, since every Player starts from the same biome values.")]
    public async Task ShouldReturnTheBaseValuesWhenThePlayerHoldsNoModifiersAsync()
    {
        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(400)));
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(100)));
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Fodder), Is.EqualTo(ScaledAmount.FromWhole(1_000)));
            Assert.That(values.GetValue(BiomeType.Forest, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(300)));
        }
    }

    [Test, Description("A modifier with no biome applies to every biome, which is what lets one tech advance raise a resource empire-wide in a single row.")]
    public async Task ShouldApplyAnAllBiomesModifierToEveryBiomeAsync()
    {
        await SeedModifiersAsync(BuildModifier(_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Forage, biome: null, flatWhole: 50, percentWhole: 0));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(450)));
            Assert.That(values.GetValue(BiomeType.Forest, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(350)));
        }
    }

    [Test, Description("A modifier naming a biome and a resource changes only that value, leaving other biomes and the biome's other resources at their base.")]
    public async Task ShouldApplyABiomeModifierOnlyToThatBiomeAndResourceAsync()
    {
        await SeedModifiersAsync(BuildModifier(_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Hunt, BiomeType.Grassland, flatWhole: 100, percentWhole: 0));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(200)));
            Assert.That(values.GetValue(BiomeType.Forest, SustenanceResource.Hunt), Is.EqualTo(ScaledAmount.FromWhole(150)), "Another biome is untouched.");
            Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(400)), "Another resource is untouched.");
        }
    }

    [Test, Description("Percentages from several sources add together before being applied once to the base: +10% and +20% of 400 is 520, where compounding would give 528.")]
    public async Task ShouldAddPercentagesTogetherRatherThanCompoundingThemAsync()
    {
        await SeedModifiersAsync(
            BuildModifier(_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Forage, biome: null, flatWhole: 0, percentWhole: 10),
            BuildModifier(_PLAYER_ID, _SECOND_SOURCE_ID, SustenanceResource.Forage, BiomeType.Grassland, flatWhole: 0, percentWhole: 20));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(520)));
    }

    [Test, Description("One modifier can carry both parts, and both are applied: +25% of 400 and +50 is 550.")]
    public async Task ShouldApplyBothPartsWhenOneModifierCarriesAFlatAndAPercentageAmountAsync()
    {
        await SeedModifiersAsync(BuildModifier(_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Forage, BiomeType.Grassland, flatWhole: 50, percentWhole: 25));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(550)));
    }

    [Test, Description("A resource the biome does not have stays null even under a modifier naming it, because null means it can never gain a value.")]
    public async Task ShouldKeepANullBaseNullWhenAModifierNamesItAsync()
    {
        await SeedModifiersAsync(BuildModifier(_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Fish, biome: null, flatWhole: 10, percentWhole: 0));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Fish), Is.Null);
    }

    [Test, Description("Another Player's modifiers are ignored, because each Player's values are their own and must not leak into anyone else's.")]
    public async Task ShouldIgnoreAnotherPlayersModifiersAsync()
    {
        await SeedModifiersAsync(BuildModifier(_OTHER_PLAYER_ID, _FIRST_SOURCE_ID, SustenanceResource.Forage, biome: null, flatWhole: 50, percentWhole: 0));

        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        Assert.That(values.GetValue(BiomeType.Grassland, SustenanceResource.Forage), Is.EqualTo(ScaledAmount.FromWhole(400)));
    }

    [Test, Description("A biome with no catalogue row has no resources at all, so its values read as null rather than as zero food.")]
    public async Task ShouldReturnNullWhenTheBiomeHasNoCatalogueRowAsync()
    {
        PlayerSustenanceValues values = await ReadValuesAsync(_PLAYER_ID);

        Assert.That(values.GetValue(BiomeType.Desert, SustenanceResource.Forage), Is.Null);
    }

    // ---------------------------------------------------------------------

    private async Task<PlayerSustenanceValues> ReadValuesAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetValuesAsync(context, playerId);
    }

    private async Task SeedModifiersAsync(params PlayerSustenanceModifier[] modifiers)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.AddRange(modifiers);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A biome with the given sustenance values, in whole numbers, and no Fish.
    /// </summary>
    private static CampaignBiome BuildBiome(BiomeType biomeType, string name, int forage, int hunt, int fodder)
    {
        return new CampaignBiome
        {
            BiomeTypeId = biomeType,
            Name = name,
            MovementCost = 1m,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342",
            BaseForageValue = ScaledAmount.FromWhole(forage).RawValue,
            BaseHuntValue = ScaledAmount.FromWhole(hunt).RawValue,
            BaseFishValue = null,
            BaseFodderValue = ScaledAmount.FromWhole(fodder).RawValue
        };
    }

    /// <summary>
    /// A Technology modifier, with its amounts given as a whole number and a whole percentage.
    /// </summary>
    private static PlayerSustenanceModifier BuildModifier(int playerId, int sourceId, SustenanceResource resource, BiomeType? biome, int flatWhole, int percentWhole)
    {
        return new PlayerSustenanceModifier
        {
            GameId = _GAME_ID,
            PlayerId = playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = sourceId,
            Resource = resource,
            BiomeTypeId = biome,
            FlatValue = ScaledAmount.FromWhole(flatWhole).RawValue,
            PercentValue = ScaledPercent.FromWholePercent(percentWhole).RawValue
        };
    }
}
