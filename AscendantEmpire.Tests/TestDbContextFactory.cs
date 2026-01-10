using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests;

/// <summary>
/// Simple IDbContextFactory for tests that creates a new context each time,
/// but reuses the same DbContextOptions (so it points at the same InMemory store).
/// </summary>
internal sealed class TestDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;

    public TestDbContextFactory(DbContextOptions<TContext> options)
    {
        _options = options;
    }

    public TContext CreateDbContext()
    {
        // This assumes your DbContext has a ctor like: (DbContextOptions<AscendantEmpireContext> options)
        return (TContext)System.Activator.CreateInstance(typeof(TContext), _options)!;
    }

    // Some EF Core versions use CreateDbContextAsync; adding it here keeps your handler happy regardless.
    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
