using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests for <see cref="PlayerTechnologySustenanceModifierService"/>: that a Player holds exactly the sustenance modifiers their discovered technologies grant.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The service is LINQ reads and tracked writes, with no raw SQL, bulk update or transaction.
/// </para>
/// <para>
/// It never saves, so every helper here commits on the service's behalf, the way a real caller does at the end of its own unit of work.
/// </para>
/// </remarks>
[TestFixture]
internal class PlayerTechnologySustenanceModifierServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;

    private readonly PlayerTechnologySustenanceModifierService _service =
        new(NullLogger<PlayerTechnologySustenanceModifierService>.Instance);

    private int _playerId;
    private int _otherPlayerId;

    [SetUp]
    public async Task SeedPlayersAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Player player = BuildPlayer("Tester");
        Player otherPlayer = BuildPlayer("Rival");

        context.Players.AddRange(player, otherPlayer);

        await context.SaveChangesAsync();

        _playerId = player.Id;
        _otherPlayerId = otherPlayer.Id;
    }

    #region Granting

    [Test, Description("A discovered technology's grants each become one modifier row sourced to that technology, which is the whole point of the reconcile.")]
    public async Task ShouldAddRowsWhenTechnologyIsDiscovered()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await AddGrantAsync(technologyId, SustenanceResource.Forage, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        int changed = await ReconcilePlayerAsync();

        List<PlayerSustenanceModifier> modifiers = await ListModifiersAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(modifiers, Has.Count.EqualTo(2));
            Assert.That(modifiers.Select(modifier => modifier.SourceType), Is.All.EqualTo(ModifierSourceType.Technology));
            Assert.That(modifiers.Select(modifier => modifier.SourceId), Is.All.EqualTo(technologyId));
            Assert.That(modifiers.Select(modifier => modifier.GameId), Is.All.EqualTo(_GAME_ID));
            Assert.That(
                modifiers.Select(modifier => modifier.Resource),
                Is.EquivalentTo(new[] { SustenanceResource.Hunt, SustenanceResource.Forage }));
            Assert.That(modifiers.Select(modifier => modifier.PercentValue), Is.All.EqualTo(200));
        }
    }

    [Test, Description("A penalty grant copies through with its sign intact, since the floors belong to the value calculator rather than to storage.")]
    public async Task ShouldCopyNegativeValuesWhenGrantIsAPenalty()
    {
        int technologyId = await AddTechnologyAsync("Stone.Overhunting");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, flatWhole: -5, percentWhole: -20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        PlayerSustenanceModifier modifier = (await ListModifiersAsync(_playerId)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifier.FlatValue, Is.EqualTo(-5_000));
            Assert.That(modifier.PercentValue, Is.EqualTo(-200));
        }
    }

    [Test, Description("A grant with no biome becomes one all-biome row rather than one row per biome, which is what keeps an empire-wide bonus a single row.")]
    public async Task ShouldAddRowForEveryBiomeWhenGrantHasNullBiome()
    {
        int technologyId = await AddTechnologyAsync("Stone.Foraging");
        await AddGrantAsync(technologyId, SustenanceResource.Forage, biome: null, flatWhole: 5);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        PlayerSustenanceModifier modifier = (await ListModifiersAsync(_playerId)).Single();

        Assert.That(modifier.BiomeTypeId, Is.Null);
    }

    [Test, Description("Two technologies granting the same resource and biome give two rows, because each source's contribution has to be losable on its own.")]
    public async Task ShouldAddRowsFromBothWhenTwoTechnologiesGrantSameResourceAndBiome()
    {
        int firstId = await AddTechnologyAsync("Stone.Hunting");
        int secondId = await AddTechnologyAsync("Stone.Tracking");
        await AddGrantAsync(firstId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await AddGrantAsync(secondId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 10);
        await SetStatusAsync(_playerId, firstId, TechnologyStatus.Discovered);
        await SetStatusAsync(_playerId, secondId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        List<PlayerSustenanceModifier> modifiers = await ListModifiersAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(modifiers, Has.Count.EqualTo(2));
            Assert.That(modifiers.Select(modifier => modifier.SourceId), Is.EquivalentTo(new[] { firstId, secondId }));
        }
    }

    #endregion

    #region Convergence

    [Test, Description("A second reconcile over unchanged state changes nothing, which is what makes the service safe to call on every discovery and every turn.")]
    public async Task ShouldChangeNothingWhenRunTwice()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        int changed = await ReconcilePlayerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.Zero);
            Assert.That(await ListModifiersAsync(_playerId), Has.Count.EqualTo(1));
        }
    }

    [Test, Description("Re-pricing a grant corrects the Player's existing row, which is the reason this converges rather than firing once on discovery.")]
    public async Task ShouldUpdateValuesWhenGrantIsRepriced()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        await RepriceGrantAsync(technologyId, SustenanceResource.Hunt, percentWhole: 35);

        int changed = await ReconcilePlayerAsync();

        List<PlayerSustenanceModifier> modifiers = await ListModifiersAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(modifiers, Has.Count.EqualTo(1), "the row is corrected rather than duplicated");
            Assert.That(modifiers.Single().PercentValue, Is.EqualTo(350));
        }
    }

    [Test, Description("Withdrawing a grant from the catalogue takes the Player's row with it, so a designer's edit cannot leave a bonus nothing grants.")]
    public async Task ShouldRemoveRowWhenGrantIsWithdrawnFromCatalogue()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        await RemoveGrantsAsync(technologyId);

        int changed = await ReconcilePlayerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(await ListModifiersAsync(_playerId), Is.Empty);
        }
    }

    [Test, Description("A modifier whose technology has left the catalogue is removed, since the catalogue is the authority on what a Player can hold.")]
    public async Task ShouldRemoveRowWhenTechnologyLeavesCatalogue()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);

        await ReconcilePlayerAsync();

        await RemoveTechnologyAsync(technologyId);

        int changed = await ReconcilePlayerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(await ListModifiersAsync(_playerId), Is.Empty);
        }
    }

    #endregion

    #region Scope

    [Test, Description("An Unlocked technology grants nothing: the acquisition cost is paid but the research is not done.")]
    public async Task ShouldAddNothingWhenTechnologyIsOnlyUnlocked()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Unlocked);

        int changed = await ReconcilePlayerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.Zero);
            Assert.That(await ListModifiersAsync(_playerId), Is.Empty);
        }
    }

    [TestCase(TechnologyStatus.Locked, Description = "A Locked technology grants nothing.")]
    [TestCase(TechnologyStatus.Available, Description = "An Available technology grants nothing.")]
    public async Task ShouldAddNothingWhenTechnologyIsNotDiscovered(TechnologyStatus status)
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, status);

        int changed = await ReconcilePlayerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.Zero);
            Assert.That(await ListModifiersAsync(_playerId), Is.Empty);
        }
    }

    [Test, Description("Modifiers from a Specialist or a Department survive a reconcile that wants none, because this service owns only the Technology source.")]
    public async Task ShouldLeaveRowsAloneWhenSourceIsSpecialistOrDepartment()
    {
        await AddModifierAsync(ModifierSourceType.Specialist, sourceId: 77);
        await AddModifierAsync(ModifierSourceType.Department, sourceId: 88);

        int changed = await ReconcilePlayerAsync();

        List<PlayerSustenanceModifier> modifiers = await ListModifiersAsync(_playerId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.Zero);
            Assert.That(
                modifiers.Select(modifier => modifier.SourceType),
                Is.EquivalentTo(new[] { ModifierSourceType.Specialist, ModifierSourceType.Department }));
        }
    }

    [Test, Description("Reconciling a Game touches every Player in it, so a catalogue change does not need a per-Player call to reach them all.")]
    public async Task ShouldReconcileEveryPlayerWhenGameIsReconciled()
    {
        int technologyId = await AddTechnologyAsync("Stone.BiomeSkill.Arid");
        await AddGrantAsync(technologyId, SustenanceResource.Hunt, BiomeType.Arid, percentWhole: 20);
        await SetStatusAsync(_playerId, technologyId, TechnologyStatus.Discovered);
        await SetStatusAsync(_otherPlayerId, technologyId, TechnologyStatus.Available);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        int changed = await _service.ReconcileGameAsync(context, _GAME_ID, CancellationToken.None);
        await context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(await ListModifiersAsync(_playerId), Has.Count.EqualTo(1));
            Assert.That(await ListModifiersAsync(_otherPlayerId), Is.Empty);
        }
    }

    #endregion

    #region Helpers

    private static Player BuildPlayer(string rulerName)
    {
        return new Player
        {
            GameId = _GAME_ID,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = rulerName,
            FactionName = $"{rulerName}s",
            PeopleName = $"{rulerName} People",
            Demonym = $"{rulerName}ian",
            Language = $"{rulerName}ish",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active
        };
    }

    private async Task<int> AddTechnologyAsync(string code)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var technology = new Technology
        {
            Code = code,
            DisplayName = code,
            IconPath = string.Empty,
            Era = TechnologyEra.Stone,
            SortOrder = await context.Technologies.CountAsync() + 1,
            DirectedRequired = 100
        };

        context.Technologies.Add(technology);
        await context.SaveChangesAsync();

        return technology.Id;
    }

    private async Task AddGrantAsync(
        int technologyId,
        SustenanceResource resource,
        BiomeType? biome,
        int flatWhole = 0,
        int percentWhole = 0)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.Add(new TechnologySustenanceModifierGrant
        {
            TechnologyId = technologyId,
            Resource = resource,
            BiomeTypeId = biome,
            FlatValue = ScaledAmount.FromWhole(flatWhole).RawValue,
            PercentValue = ScaledPercent.FromWholePercent(percentWhole).RawValue
        });

        await context.SaveChangesAsync();
    }

    private async Task RepriceGrantAsync(int technologyId, SustenanceResource resource, int percentWhole)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TechnologySustenanceModifierGrant grant = await context.TechnologySustenanceModifierGrants
            .SingleAsync(row => row.TechnologyId == technologyId && row.Resource == resource);

        grant.PercentValue = ScaledPercent.FromWholePercent(percentWhole).RawValue;

        await context.SaveChangesAsync();
    }

    private async Task RemoveGrantsAsync(int technologyId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TechnologySustenanceModifierGrants.RemoveRange(
            context.TechnologySustenanceModifierGrants.Where(row => row.TechnologyId == technologyId));

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a technology and its grants, standing in for a designer dropping it from the catalogue.
    /// </summary>
    private async Task RemoveTechnologyAsync(int technologyId)
    {
        await RemoveGrantsAsync(technologyId);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Technologies.RemoveRange(context.Technologies.Where(technology => technology.Id == technologyId));

        await context.SaveChangesAsync();
    }

    private async Task SetStatusAsync(int playerId, int technologyId, TechnologyStatus status)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerTechnologies.Add(new PlayerTechnology
        {
            PlayerId = playerId,
            TechnologyId = technologyId,
            GameId = _GAME_ID,
            Status = status,
            DirectedRequired = 100,
            DirectedPoints = 0,
            ExperiencePoints = 0,
            DiffusionPoints = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task AddModifierAsync(ModifierSourceType sourceType, int sourceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerSustenanceModifiers.Add(new PlayerSustenanceModifier
        {
            GameId = _GAME_ID,
            PlayerId = _playerId,
            SourceType = sourceType,
            SourceId = sourceId,
            Resource = SustenanceResource.Hunt,
            BiomeTypeId = BiomeType.Arid,
            FlatValue = 0,
            PercentValue = 200
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Reconciles the Player and commits, since the service joins its caller's unit of work and never saves.
    /// </summary>
    private async Task<int> ReconcilePlayerAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int changed = await _service.ReconcileAsync(context, _GAME_ID, _playerId, CancellationToken.None);

        await context.SaveChangesAsync();

        return changed;
    }

    private async Task<List<PlayerSustenanceModifier>> ListModifiersAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.PlayerSustenanceModifiers
            .AsNoTracking()
            .Where(modifier => modifier.PlayerId == playerId)
            .ToListAsync();
    }

    #endregion
}
