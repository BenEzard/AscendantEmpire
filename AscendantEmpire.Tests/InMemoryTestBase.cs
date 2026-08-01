using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace AscendantEmpire.Tests;

/// <summary>
/// Base class for tests that need a database, but only one an in-memory provider can satisfy.
/// </summary>
/// <remarks>
/// <para>
/// This is the middle tier described in <c>testing.md</c>: use it whenever the exercised code path sticks to ordinary EF Core operations — LINQ queries,
/// <c>Add</c>/<c>AddRange</c>/<c>RemoveRange</c> and <c>SaveChanges</c>. It is deliberately cheaper than <see cref="SqlServerTestBase"/>, which spins up a
/// real SQL Server container.
/// </para>
/// <para>
/// The in-memory provider cannot reproduce these, so a test that depends on any of them belongs on <see cref="SqlServerTestBase"/> instead:
/// <list type="bullet">
///   <item><description>Database-enforced constraints — unique indexes, foreign keys, check constraints.</description></item>
///   <item><description><c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>.</description></item>
///   <item><description>Raw SQL — <c>FromSqlRaw</c>, <c>ExecuteSqlRaw</c>, <c>ExecuteSqlInterpolated</c>.</description></item>
///   <item><description>Explicit transactions — <c>BeginTransactionAsync</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// To use: inherit from this class and resolve contexts through the <see cref="DbFactory"/> property. Each test gets its own isolated store, so no cleanup
/// or ordering assumptions are needed.
/// </para>
/// </remarks>
public abstract class InMemoryTestBase
{
    /// <summary>
    /// The root that owns every in-memory store created by this base class.
    /// </summary>
    /// <remarks>
    /// Sharing one root across all tests matters for more than tidiness: EF keys its internal service provider off the root, so a single root means a
    /// single provider for the whole run. Give each test its own root and EF builds a fresh provider per test and starts warning once enough pile up.
    /// </remarks>
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    /// <summary>
    /// A context factory backed by an in-memory store that is created fresh before each test and discarded after.
    /// </summary>
    protected IDbContextFactory<AscendantEmpireContext> DbFactory { get; private set; } = null!;

    [SetUp]
    public async Task InMemoryBaseSetUp()
    {
        // A unique store name per test gives full isolation, so tests can run in any order without leaking state into one another.
        string storeName = $"AscendantEmpire_Test_{Guid.NewGuid():N}";

        DbContextOptions<AscendantEmpireContext> options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(storeName, DatabaseRoot)
            .AddInterceptors(new RowVersionInterceptor())
            .Options;

        DbFactory = new TestDbContextFactory<AscendantEmpireContext>(options);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task InMemoryBaseTearDown()
    {
        // Drop the store so its rows don't sit in the shared root for the rest of the run.
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// Fills in <c>[Timestamp]</c> concurrency tokens on save, standing in for the <c>rowversion</c> values SQL Server would generate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Entities such as <c>TurnQueue</c> declare a required <c>byte[] RowVersion</c> that the database populates on insert. The in-memory provider
    /// generates nothing, so an otherwise valid insert is rejected with "Required properties are missing" — a failure about the provider, not about the
    /// code under test. Stamping the value here keeps seed code free of a workaround that would have to be repeated in every fixture.
    /// </para>
    /// <para>
    /// Only store-generated <c>byte[]</c> concurrency tokens that are still null are touched, so a value a test sets deliberately is always left alone.
    /// </para>
    /// </remarks>
    private sealed class RowVersionInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            StampRowVersions(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            StampRowVersions(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void StampRowVersions(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            foreach (EntityEntry entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not EntityState.Added and not EntityState.Modified)
                {
                    continue;
                }

                foreach (PropertyEntry property in entry.Properties)
                {
                    bool isStoreGeneratedRowVersion = property.Metadata.IsConcurrencyToken
                        && property.Metadata.ValueGenerated != ValueGenerated.Never
                        && property.Metadata.ClrType == typeof(byte[]);

                    if (isStoreGeneratedRowVersion && property.CurrentValue is null)
                    {
                        property.CurrentValue = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                    }
                }
            }
        }
    }
}
