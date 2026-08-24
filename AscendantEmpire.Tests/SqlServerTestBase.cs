using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AscendantEmpire.Tests;

/// <summary>
/// Base class for tests that need a real SQL Server database via Testcontainers.
/// </summary>
/// <remarks>
/// <para>
/// Each test gets its own isolated database, named after nothing but a fresh GUID, restored from the migrated template before the test runs and dropped
/// afterwards. The template is built once for the whole assembly by <see cref="MsSqlContainerFixture"/>, so the migration history is applied once per run
/// rather than once per test — see <see cref="SqlServerTemplateDatabase"/> for why that matters.
/// </para>
/// <para>
/// To use:
/// <list type="number">
/// <item><description>Inherit your test class from <see cref="SqlServerTestBase"/>.</description></item>
/// <item><description>Use the <see cref="DbFactory"/> property to resolve an <see cref="IDbContextFactory{AscendantEmpireContext}"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract class SqlServerTestBase
{
    private string _databaseName = string.Empty;
    private DbContextOptions<AscendantEmpireContext> _options = null!;

    /// <summary>
    /// A context factory backed by a real SQL Server database. The database is created fresh before each test and dropped after.
    /// </summary>
    protected IDbContextFactory<AscendantEmpireContext> DbFactory { get; private set; } = null!;

    [SetUp]
    public async Task SqlServerBaseSetUp()
    {
        // Built by the first containerised test to run; every later test finds it already there.
        await SqlServerTemplateDatabase.EnsureBuiltAsync(MsSqlContainerFixture.ConnectionString);

        // Each test gets a unique database name so tests are fully isolated.
        _databaseName = $"AscendantEmpire_Test_{Guid.NewGuid():N}";

        // Restoring the already-migrated template both creates the database and gives it the production schema, so no migration runs here.
        string connectionString = await SqlServerTemplateDatabase.RestoreCopyAsync(_databaseName);

        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseSqlServer(connectionString)
            // Suppress the warning that fires when the model has pending changes not yet captured in a migration.
            // Tests apply existing migrations only; they don't enforce that all model changes have been migrated.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        DbFactory = new TestDbContextFactory<AscendantEmpireContext>(_options);
    }

    [TearDown]
    public async Task SqlServerBaseTearDown()
    {
        // Drop the per-test database to keep the container clean.
        await using var context = await DbFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
    }
}
