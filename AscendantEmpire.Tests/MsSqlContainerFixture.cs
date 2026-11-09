using System.Diagnostics;

using Testcontainers.MsSql;

namespace AscendantEmpire.Tests;

/// <summary>
/// Assembly-wide NUnit setup fixture that starts a single SQL Server container before any tests run and disposes it once all tests have finished.
/// </summary>
/// <remarks>
/// <para>
/// Usage in a test class: inherit from <see cref="SqlServerTestBase"/> to get a pre-migrated <see cref="IDbContextFactory{AscendantEmpireContext}"/> that
/// is isolated per test.
/// </para>
/// <para>
/// This fixture only owns the container. The migrated database inside it is built by <see cref="SqlServerTemplateDatabase"/>, lazily, when the first
/// containerised test asks for one — so a run that selects no such test starts the container but never migrates anything.
/// </para>
/// </remarks>
[SetUpFixture]
public sealed class MsSqlContainerFixture
{
    private static MsSqlContainer? _container;

    /// <summary>
    /// The connection string for the running SQL Server container. Available after <see cref="OneTimeSetUp"/> has completed.
    /// </summary>
    public static string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("SQL Server container has not been started.");

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await EnsureDockerDesktopRunningAsync();

        // Docker.DotNet (used by Testcontainers) advertises a newer client API version than some
        // engines support, producing "client version X is too new" errors. Pin the client to the
        // engine's reported API version so negotiation succeeds regardless of engine age.
        await PinDockerApiVersionAsync();

        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPortBinding(14330, MsSqlBuilder.MsSqlPort)
            .Build();

        await _container.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    /// <summary>
    /// Ensures Docker Desktop is running before attempting to start a container.
    /// If the Docker engine is not reachable, launches Docker Desktop and waits up to 60 seconds for it to become ready.
    /// </summary>
    private static async Task EnsureDockerDesktopRunningAsync()
    {
        if (await IsDockerReadyAsync())
            return;

        // Docker Desktop is not running — start it and wait for the engine to become available.
        string dockerDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker", "Docker", "Docker Desktop.exe");

        if (!File.Exists(dockerDesktopPath))
            throw new InvalidOperationException($"Docker Desktop was not found at '{dockerDesktopPath}' and the Docker engine is not running. Please start Docker Desktop manually.");

        Process.Start(new ProcessStartInfo(dockerDesktopPath) { UseShellExecute = true });

        // Poll until the Docker engine responds or the timeout elapses.
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        while (!await IsDockerReadyAsync())
        {
            if (cts.Token.IsCancellationRequested)
                throw new TimeoutException("Docker Desktop was launched but the Docker engine did not become ready within 60 seconds.");

            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        }
    }

    /// <summary>
    /// Pins the Docker.DotNet client to the running engine's API version via the <c>DOCKER_API_VERSION</c>
    /// environment variable, avoiding "client version is too new" failures against older engines.
    /// </summary>
    /// <remarks>
    /// Respects an existing <c>DOCKER_API_VERSION</c> if the caller has already set one (e.g. in CI).
    /// Falls back silently if the engine version cannot be read — the container build will surface any real error.
    /// </remarks>
    private static async Task PinDockerApiVersionAsync()
    {
        // Don't override an explicit choice from the environment.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_API_VERSION")))
        {
            return;
        }

        string? serverApiVersion = await GetDockerServerApiVersionAsync();
        if (!string.IsNullOrWhiteSpace(serverApiVersion))
        {
            Environment.SetEnvironmentVariable("DOCKER_API_VERSION", serverApiVersion);
        }
    }

    /// <summary>
    /// Reads the Docker engine's supported API version via the CLI (e.g. "1.41"), or <see langword="null"/> if it cannot be determined.
    /// </summary>
    private static async Task<string?> GetDockerServerApiVersionAsync()
    {
        try
        {
            ProcessStartInfo psi = new("docker", "version --format {{.Server.APIVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the Docker engine is reachable via the CLI.
    /// </summary>
    private static async Task<bool> IsDockerReadyAsync()
    {
        try
        {
            ProcessStartInfo psi = new("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process is null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
