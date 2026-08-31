using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;

namespace AscendantEmpire.Tests.UnitDesigns.Designer;

/// <summary>
/// Tests for the name-matching rules the unit-template save handlers share: which design a name belongs to, and how many troops are fielded from it.
/// </summary>
/// <remarks>
/// Tier 2 (see testing.md): the helper is plain LINQ over <c>UnitTemplate</c> and <c>UnitInstance</c> scalars — no transaction, no complex type and no
/// reliance on database-enforced constraints, so an in-memory store is enough. What the handlers then *do* with a match is covered by
/// <see cref="UnitTemplateOverwriteTests"/>, which needs the containerised tier. Templates are seeded with bare foreign-key values rather than real
/// components and players, since none of these lookups follow those navigations.
/// </remarks>
internal class UnitTemplateNameLookupTests : InMemoryTestBase
{
    private const int GAME_ID = 1;
    private const int PLAYER_ID = 2;
    private const int OTHER_PLAYER_ID = 3;

    private int _customTemplateId;
    private int _globalTemplateId;

    /// <summary>
    /// Seeds three templates covering every distinction the lookups draw: the player's own "Spearman", a global "Legionary", and another player's
    /// "Spearman". Two troops are fielded from the player's own template, and one from the global.
    /// </summary>
    private async Task SeedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var customTemplate = new UnitTemplate
        {
            Code = null,
            DisplayName = "Spearman",
            MapImagePath = "spearman.png",
            GameId = GAME_ID,
            PlayerId = PLAYER_ID,
            BodyComponentId = 10,
        };

        var globalTemplate = new UnitTemplate
        {
            Code = "legionary",
            DisplayName = "Legionary",
            MapImagePath = "legionary.png",
            BodyComponentId = 10,
        };

        // Same name, different owner — proves the player lookup is scoped to the caller rather than matching on the name alone.
        var otherPlayerTemplate = new UnitTemplate
        {
            Code = null,
            DisplayName = "Spearman",
            MapImagePath = "spearman.png",
            GameId = GAME_ID,
            PlayerId = OTHER_PLAYER_ID,
            BodyComponentId = 10,
        };

        context.UnitTemplates.AddRange(customTemplate, globalTemplate, otherPlayerTemplate);
        await context.SaveChangesAsync();

        context.UnitInstances.AddRange(
            BuildTroop(customTemplate.Id),
            BuildTroop(customTemplate.Id),
            // A troop on a different template, so a count can't accidentally be "every troop in the game".
            BuildTroop(globalTemplate.Id));
        await context.SaveChangesAsync();

        _customTemplateId = customTemplate.Id;
        _globalTemplateId = globalTemplate.Id;
    }

    /// <summary>
    /// Builds a minimal fielded troop for a template. Only the template link matters here; the rest is filler the entity requires.
    /// </summary>
    private static UnitInstance BuildTroop(int templateId) => new()
    {
        GameId = GAME_ID,
        PlayerId = PLAYER_ID,
        CampaignMapTileId = 1,
        UnitTemplateId = templateId,
        InitialStrength = 10,
        HealthyCount = 10,
    };

    [Test, Description("A name no design of the player's holds comes back unmatched.")]
    public async Task PlayerLookupReturnsNullForAnUnusedNameAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? found = await UnitTemplateNameLookup.FindPlayerTemplateByNameAsync(
            context, GAME_ID, PLAYER_ID, "Archer", excludeTemplateId: null, CancellationToken.None);

        Assert.That(found, Is.Null);
    }

    [Test, Description("Names match trimmed and case-insensitively, so near-identical names can't slip past as new designs.")]
    public async Task PlayerLookupMatchesIgnoringCaseAndSurroundingWhitespaceAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? found = await UnitTemplateNameLookup.FindPlayerTemplateByNameAsync(
            context, GAME_ID, PLAYER_ID, "  sPeArMaN  ", excludeTemplateId: null, CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(_customTemplateId));
    }

    [Test, Description("Another player's design with the same name is not a match — custom names are only unique per player.")]
    public async Task PlayerLookupIsScopedToTheOwnerAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? found = await UnitTemplateNameLookup.FindPlayerTemplateByNameAsync(
            context, GAME_ID, OTHER_PLAYER_ID, "Spearman", excludeTemplateId: null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.Not.Null, "The other player has a design of that name too.");
            Assert.That(found!.Id, Is.Not.EqualTo(_customTemplateId), "But it is theirs, not the first player's.");
        }
    }

    [Test, Description("Excluding a design exempts it from its own name, so an update that keeps its name isn't a clash with itself.")]
    public async Task PlayerLookupHonoursTheExclusionAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? found = await UnitTemplateNameLookup.FindPlayerTemplateByNameAsync(
            context, GAME_ID, PLAYER_ID, "Spearman", excludeTemplateId: _customTemplateId, CancellationToken.None);

        Assert.That(found, Is.Null);
    }

    [Test, Description("The global lookup finds a shared design by name and ignores player-owned ones.")]
    public async Task GlobalLookupMatchesOnlyGamelessTemplatesAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? legionary = await UnitTemplateNameLookup.FindGlobalTemplateByNameAsync(context, "legionary", CancellationToken.None);
        UnitTemplate? spearman = await UnitTemplateNameLookup.FindGlobalTemplateByNameAsync(context, "Spearman", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(legionary?.Id, Is.EqualTo(_globalTemplateId), "Matched case-insensitively.");
            Assert.That(spearman, Is.Null, "The player-owned designs of that name are not global.");
        }
    }

    [Test, Description("The dev flow's lookup matches on the Code, which is its unique natural key.")]
    public async Task GlobalCodeLookupMatchesOnCodeAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitTemplate? found = await UnitTemplateNameLookup.FindGlobalTemplateByCodeAsync(context, "legionary", CancellationToken.None);
        UnitTemplate? missing = await UnitTemplateNameLookup.FindGlobalTemplateByCodeAsync(context, "scout_walker", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found?.Id, Is.EqualTo(_globalTemplateId));
            Assert.That(missing, Is.Null);
        }
    }

    [Test, Description("Troops are counted per template, not per game, so one design's troops never inflate another's count.")]
    public async Task TroopCountIsScopedToTheTemplateAsync()
    {
        await SeedAsync();
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int custom = await UnitTemplateNameLookup.CountUnitInstancesAsync(context, _customTemplateId, CancellationToken.None);
        int global = await UnitTemplateNameLookup.CountUnitInstancesAsync(context, _globalTemplateId, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(custom, Is.EqualTo(2));
            Assert.That(global, Is.EqualTo(1));
        }
    }
}
