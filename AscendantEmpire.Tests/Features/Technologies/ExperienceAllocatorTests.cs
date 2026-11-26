using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Domain.Entities.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Technologies;

/// <summary>
/// Tests for <see cref="ExperienceAllocator"/> — the only thing that writes <c>technology.PendingExperienceAccrual</c>.
/// </summary>
/// <remarks>
/// On the in-memory tier. The allocator writes through the change tracker rather than with <c>ExecuteUpdateAsync</c>, precisely so an accrual can join its
/// caller's transaction, and it saves nothing itself — so each test saves and then reads back, which is what the real callers do.
/// </remarks>
[TestFixture]
internal class ExperienceAllocatorTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 7;

    private const int _EXPLORATION_TYPE_ID = 100;
    private const int _JUNGLE_TYPE_ID = 101;

    #region Inserting and incrementing

    [Test, Description("A first accrual for a type inserts the row, because the accumulator starts empty between Turns and a producer should not have to know that.")]
    public async Task ShouldInsertTheRowWhenNoAccrualExistsForTheTypeAsync()
    {
        await SeedCatalogueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await NewAllocator().AccrueAsync(
            context,
            _GAME_ID,
            _PLAYER_ID,
            new Dictionary<string, int> { [ExperienceTypeCodes.EXPLORATION] = 3 },
            CancellationToken.None);

        await context.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(PointsFor(_EXPLORATION_TYPE_ID), Is.EqualTo(3));
        });
    }

    [Test, Description("A second accrual for the same type increments the existing row, which is what keeps a Force crossing ten tiles to one row rather than ten.")]
    public async Task ShouldIncrementTheExistingRowWhenTheTypeHasAlreadyAccruedAsync()
    {
        await SeedCatalogueAsync();
        await AccrueAndSaveAsync(ExperienceTypeCodes.EXPLORATION, 3);
        await AccrueAndSaveAsync(ExperienceTypeCodes.EXPLORATION, 4);

        Assert.That(PointsFor(_EXPLORATION_TYPE_ID), Is.EqualTo(7));
    }

    [Test, Description("Several codes in one batch each get their own row, so a Turn's worth of biomes is one call rather than one call per biome.")]
    public async Task ShouldWriteEveryCodeInTheBatchAsync()
    {
        await SeedCatalogueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await NewAllocator().AccrueAsync(
            context,
            _GAME_ID,
            _PLAYER_ID,
            new Dictionary<string, int> { [ExperienceTypeCodes.EXPLORATION] = 3, ["Biome.Jungle"] = 2 },
            CancellationToken.None);

        await context.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(PointsFor(_EXPLORATION_TYPE_ID), Is.EqualTo(3));
            Assert.That(PointsFor(_JUNGLE_TYPE_ID), Is.EqualTo(2));
        });
    }

    #endregion

    #region Refusing

    [Test, Description("An unrecognised code fails the whole batch and writes nothing, so a typo cannot silently swallow points no later report could miss.")]
    public async Task ShouldFailAndWriteNothingWhenACodeIsNotInTheCatalogueAsync()
    {
        await SeedCatalogueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase result = await NewAllocator().AccrueAsync(
            context,
            _GAME_ID,
            _PLAYER_ID,
            new Dictionary<string, int> { [ExperienceTypeCodes.EXPLORATION] = 3, ["Biome.Nowhere"] = 2 },
            CancellationToken.None);

        await context.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(PointsFor(_EXPLORATION_TYPE_ID), Is.Zero, "the recognised code must not be written either - the batch is refused whole");
        });
    }

    [Test, Description("A zero-point entry writes no row, so an action that happened to earn nothing leaves no trace to be mistaken for one that did.")]
    public async Task ShouldWriteNothingWhenEveryEntryIsZeroAsync()
    {
        await SeedCatalogueAsync();
        await AccrueAndSaveAsync(ExperienceTypeCodes.EXPLORATION, 0);

        Assert.That(RowCountFor(_EXPLORATION_TYPE_ID), Is.Zero, "No row at all, rather than a row of zero for the drain to pick up and divide.");
    }

    #endregion

    #region Helpers

    private ExperienceAllocator NewAllocator()
    {
        return new ExperienceAllocator(NullLogger<ExperienceAllocator>.Instance);
    }

    private async Task SeedCatalogueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.ExperienceTypes.AddRange(
            NewExperienceType(_EXPLORATION_TYPE_ID, ExperienceTypeCodes.EXPLORATION),
            NewExperienceType(_JUNGLE_TYPE_ID, "Biome.Jungle"));

        await context.SaveChangesAsync();
    }

    private static ExperienceType NewExperienceType(int id, string code)
    {
        return new ExperienceType
        {
            Id = id,
            Code = code,
            DisplayName = code,
            IconPath = string.Empty,
            SortOrder = id
        };
    }

    private async Task AccrueAndSaveAsync(string code, int points)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await NewAllocator().AccrueAsync(
            context,
            _GAME_ID,
            _PLAYER_ID,
            new Dictionary<string, int> { [code] = points },
            CancellationToken.None);

        await context.SaveChangesAsync();
    }

    /// <summary>How many accrual rows the Player holds for one type, which tells a row of zero apart from no row at all.</summary>
    private int RowCountFor(int experienceTypeId)
    {
        using AscendantEmpireContext context = DbFactory.CreateDbContext();

        return context.PendingExperienceAccruals.Count(accrual => accrual.PlayerId == _PLAYER_ID && accrual.ExperienceTypeId == experienceTypeId);
    }

    private int PointsFor(int experienceTypeId)
    {
        using AscendantEmpireContext context = DbFactory.CreateDbContext();

        return context.PendingExperienceAccruals
            .Where(accrual => accrual.PlayerId == _PLAYER_ID && accrual.ExperienceTypeId == experienceTypeId)
            .Select(accrual => accrual.Points)
            .FirstOrDefault();
    }

    #endregion
}
