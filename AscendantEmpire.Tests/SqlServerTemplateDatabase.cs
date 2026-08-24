using AscendantEmpire.Application.Data;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AscendantEmpire.Tests;

/// <summary>
/// Migrates one "template" database inside the test SQL Server container, backs it up once, and hands out per-test copies restored from that backup.
/// </summary>
/// <remarks>
/// <para>
/// Applying the migration history costs seconds per database, and most of that is one migration: <c>PopulateNumbersTable</c> writes 1,000,001 rows into
/// <c>util.Numbers</c>. Paying that for every test in the containerised tier dominated the tier's runtime. Migrating once per assembly and restoring a
/// backup per test gives each test the same pristine database — schema, migration history, seeded rows and identity seeds alike — for the cost of a file
/// copy inside the container.
/// </para>
/// <para>
/// Isolation is deliberately unchanged. Every test still gets its own database, created before it runs and dropped after, so nothing a test writes can be
/// seen by the next one and tests remain free to run in any order. Only the way that database is built has changed.
/// </para>
/// <para>
/// Everything here talks to SQL Server over T-SQL rather than shelling into the container, so it works the same way against any reachable instance. The
/// paths are the Linux image's data directory, which is the one location the <c>mssql</c> service account can always write to.
/// </para>
/// </remarks>
internal static class SqlServerTemplateDatabase
{
    /// <summary>
    /// The migrated database every per-test database is copied from. Never handed to a test.
    /// </summary>
    private const string TEMPLATE_DATABASE_NAME = "AscendantEmpire_Test_Template";

    /// <summary>
    /// The container's data directory, the one path the SQL Server service account in the Linux image is guaranteed to be able to write to.
    /// </summary>
    private const string CONTAINER_DATA_DIRECTORY = "/var/opt/mssql/data";

    /// <summary>
    /// The backup taken from the template once the migrations have been applied, and the source of every per-test restore.
    /// </summary>
    private const string TEMPLATE_BACKUP_PATH = $"{CONTAINER_DATA_DIRECTORY}/{TEMPLATE_DATABASE_NAME}.bak";

    /// <summary>
    /// Generous enough for the one-off migration run, which seeds a million-row table on a cold container.
    /// </summary>
    private const int LONG_RUNNING_COMMAND_TIMEOUT_SECONDS = 300;

    /// <summary>
    /// Serialises the one-off build so that a future move to parallel fixtures cannot start two of them.
    /// </summary>
    private static readonly SemaphoreSlim BuildLock = new(1, 1);

    private static string _containerConnectionString = string.Empty;
    private static bool _isBuilt;

    // The template's logical file names, read once so that each restore can MOVE them to paths of its own. A restore has to name the logical files the
    // backup was taken from, and those names come from the template's creation rather than from the name of the copy being restored.
    private static string _dataFileLogicalName = string.Empty;
    private static string _logFileLogicalName = string.Empty;

    /// <summary>
    /// Builds the template database on first use, applying every migration to it and backing it up. Subsequent calls return immediately.
    /// </summary>
    /// <param name="containerConnectionString">The connection string reported by the running SQL Server container.</param>
    /// <remarks>
    /// This is deferred to the first containerised test rather than done alongside the container start, so a run that selects only tier 1 and tier 2 tests
    /// never pays for it.
    /// </remarks>
    public static async Task EnsureBuiltAsync(string containerConnectionString)
    {
        if (_isBuilt)
        {
            return;
        }

        await BuildLock.WaitAsync();
        try
        {
            if (_isBuilt)
            {
                return;
            }

            await BuildAsync(containerConnectionString);
            _isBuilt = true;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    /// <summary>
    /// Creates the template database, applies every migration to it, and backs it up.
    /// </summary>
    private static async Task BuildAsync(string containerConnectionString)
    {
        _containerConnectionString = containerConnectionString;

        // The container is new for every run, so the template cannot already exist — but dropping first keeps this safe against a reused instance.
        await ExecuteOnMasterAsync($"""
            IF DB_ID(N'{TEMPLATE_DATABASE_NAME}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{TEMPLATE_DATABASE_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{TEMPLATE_DATABASE_NAME}];
            END
            """);

        // SqlClient opens the connection to the Initial Catalog named in the connection string before EF Core gets a chance to run CREATE DATABASE, so the
        // database is created explicitly via master first and the migrations are pointed at it afterwards.
        await ExecuteOnMasterAsync($"CREATE DATABASE [{TEMPLATE_DATABASE_NAME}]");

        DbContextOptions<AscendantEmpireContext> options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseSqlServer(BuildConnectionString(containerConnectionString, TEMPLATE_DATABASE_NAME), sqlServer => sqlServer.CommandTimeout(LONG_RUNNING_COMMAND_TIMEOUT_SECONDS))
            // Suppress the warning that fires when the model has pending changes not yet captured in a migration.
            // Tests apply existing migrations only; they don't enforce that all model changes have been migrated.
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using (AscendantEmpireContext context = new(options))
        {
            // Apply all migrations so the schema matches production exactly. This is now the only place they run in the whole test assembly.
            await context.Database.MigrateAsync();
        }

        await ReadTemplateFileNamesAsync();
        await ShrinkTemplateLogAsync();
        await BackUpTemplateAsync();
    }

    /// <summary>
    /// Restores a fresh copy of the template under the given database name and returns a connection string pointing at it.
    /// </summary>
    /// <param name="databaseName">The per-test database name to restore into. Must not already exist.</param>
    public static async Task<string> RestoreCopyAsync(string databaseName)
    {
        if (_containerConnectionString.Length == 0)
        {
            throw new InvalidOperationException($"The template database has not been built. Call {nameof(EnsureBuiltAsync)} first.");
        }

        // MOVE re-points the template's logical files at paths belonging to this copy, which is what lets one backup be restored many times side by side.
        await ExecuteOnMasterAsync($"""
            RESTORE DATABASE [{databaseName}] FROM DISK = N'{TEMPLATE_BACKUP_PATH}'
            WITH MOVE N'{_dataFileLogicalName}' TO N'{CONTAINER_DATA_DIRECTORY}/{databaseName}.mdf',
                 MOVE N'{_logFileLogicalName}' TO N'{CONTAINER_DATA_DIRECTORY}/{databaseName}_log.ldf',
                 RECOVERY
            """);

        return BuildConnectionString(_containerConnectionString, databaseName);
    }

    /// <summary>
    /// Reads the template's data and log logical file names, which every restore has to name in its MOVE clauses.
    /// </summary>
    private static async Task ReadTemplateFileNamesAsync()
    {
        await using SqlConnection connection = new(BuildConnectionString(_containerConnectionString, TEMPLATE_DATABASE_NAME));
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name, type_desc FROM sys.database_files";

        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string logicalName = reader.GetString(0);
            string fileType = reader.GetString(1);

            if (fileType == "ROWS")
            {
                _dataFileLogicalName = logicalName;
            }
            else if (fileType == "LOG")
            {
                _logFileLogicalName = logicalName;
            }
        }

        if (_dataFileLogicalName.Length == 0 || _logFileLogicalName.Length == 0)
        {
            throw new InvalidOperationException($"Could not read the data and log file names of '{TEMPLATE_DATABASE_NAME}'.");
        }
    }

    /// <summary>
    /// Switches the template to SIMPLE recovery and shrinks its transaction log before the backup is taken.
    /// </summary>
    /// <remarks>
    /// Seeding a million-row table leaves a transaction log far larger than the data it describes. A restore has to lay that file down again for every
    /// copy, so shrinking here is what keeps a per-test restore down to a copy of the data alone.
    /// </remarks>
    private static async Task ShrinkTemplateLogAsync()
    {
        await ExecuteOnMasterAsync($"ALTER DATABASE [{TEMPLATE_DATABASE_NAME}] SET RECOVERY SIMPLE");

        // DBCC SHRINKFILE only acts on the current database, so this one has to run on a connection to the template rather than to master.
        await using SqlConnection connection = new(BuildConnectionString(_containerConnectionString, TEMPLATE_DATABASE_NAME));
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandTimeout = LONG_RUNNING_COMMAND_TIMEOUT_SECONDS;
        command.CommandText = $"DBCC SHRINKFILE (N'{_logFileLogicalName}', 1)";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Takes the single backup that every per-test database is restored from.
    /// </summary>
    private static async Task BackUpTemplateAsync()
    {
        // FORMAT and INIT overwrite rather than append, so a reused instance cannot accumulate backup sets in the one file and restore the wrong one.
        await ExecuteOnMasterAsync($"""
            BACKUP DATABASE [{TEMPLATE_DATABASE_NAME}] TO DISK = N'{TEMPLATE_BACKUP_PATH}'
            WITH FORMAT, INIT
            """);
    }

    /// <summary>
    /// Runs a statement against the container's master database.
    /// </summary>
    private static async Task ExecuteOnMasterAsync(string sql)
    {
        await using SqlConnection connection = new(BuildConnectionString(_containerConnectionString, "master"));
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandTimeout = LONG_RUNNING_COMMAND_TIMEOUT_SECONDS;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Replaces the Initial Catalog in the container's connection string with the specified database name.
    /// </summary>
    public static string BuildConnectionString(string containerConnectionString, string databaseName)
    {
        SqlConnectionStringBuilder builder = new(containerConnectionString)
        {
            InitialCatalog = databaseName,
            // Required when connecting to a SQL Server container: the container uses a self-signed certificate, and Microsoft.Data.SqlClient 4+ defaults to Encrypt=True.
            // Without this the SSL handshake fails and surfaces as "Login failed for user 'sa'".
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
