using AscendantEmpire.Application.Data;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AscendantEmpire.Tests;

/// <summary>
/// Base class for tests that need a real SQL Server database via Testcontainers.
/// 
/// Each test gets its own isolated database (named after the test) that is created with EnsureCreated before the test runs and dropped afterwards.
/// 
/// To use:
///   1. Inherit your test class from SqlServerTestBase.
///   2. Use the <see cref="DbFactory"/> field to resolve an <see cref="IDbContextFactory{AscendantEmpireContext}"/>.
/// </summary>
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
        // Each test gets a unique database name so tests are fully isolated.
        _databaseName = $"AscendantEmpire_Test_{Guid.NewGuid():N}";

        // SqlClient opens the connection to the InitialCatalog specified in the connection string before EF Core gets a chance to run CREATE DATABASE.
        // We therefore create the database explicitly via master first, then hand off to EF migrations.
        string masterConnectionString = BuildConnectionString(MsSqlContainerFixture.ConnectionString, "master");
        await using (var masterConnection = new SqlConnection(masterConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var cmd = masterConnection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        string connectionString = BuildConnectionString(MsSqlContainerFixture.ConnectionString, _databaseName);

        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseSqlServer(connectionString)
            // Suppress the warning that fires when the model has pending changes not yet captured in a migration.
            // Tests apply existing migrations only; they don't enforce that all model changes have been migrated.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        DbFactory = new TestDbContextFactory<AscendantEmpireContext>(_options);

        // Apply all migrations so the schema matches production exactly.
        await using var context = await DbFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    [TearDown]
    public async Task SqlServerBaseTearDown()
    {
        // Drop the per-test database to keep the container clean.
        await using var context = await DbFactory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// Replaces the Initial Catalog in the container's connection string with the specified database name.
    /// </summary>
    private static string BuildConnectionString(string containerConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(containerConnectionString)
        {
            InitialCatalog = databaseName,
            // Required when connecting to a SQL Server container: the container uses a self-signed certificate, and Microsoft.Data.SqlClient 4+ defaults to Encrypt=True.
            // Without this the SSL handshake fails and surfaces as "Login failed for user 'sa'".
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
