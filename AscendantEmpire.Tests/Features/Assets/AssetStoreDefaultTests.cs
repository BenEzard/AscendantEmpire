using AscendantEmpire.Application.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Pins that no property carrying a store default leaves its sentinel unstated, which EF warns about at startup.
/// </summary>
/// <remarks>
/// <para>
/// EF's sentinel is the value meaning "the author set nothing", and it defaults to the CLR default. A property with both a store default and an unstated
/// sentinel therefore has the store default written whenever it holds the CLR default — harmless where that value is not a real one, silently destructive
/// where it is. The warning cannot tell the two apart, so it fires on both and the model has to say which it is.
/// </para>
/// <para>
/// Configured to throw rather than log, so the assertion is the same thing the running API reports: validation happens on first access to the model, which
/// is what this reaches for. The event is named for <c>bool</c> because that was the original case; it covers every type whose CLR default collides.
/// </para>
/// <para>
/// No database of any kind: only the model is built, from options that are never connected.
/// </para>
/// </remarks>
[TestFixture]
internal sealed class AssetStoreDefaultTests
{
    /// <summary>
    /// A connection string that is only ever parsed, never opened, so the model can be built without a server.
    /// </summary>
    private const string _UNUSED_CONNECTION = "Server=(unused);Database=(unused);Trusted_Connection=True;";

    [Test, Description("Every store default in the model states its sentinel, so EF never silently replaces a value the author meant.")]
    public void Model_StatesASentinelForEveryStoreDefault()
    {
        DbContextOptions<AscendantEmpireContext> options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseSqlServer(_UNUSED_CONNECTION)
            .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.BoolWithDefaultWarning))
            .Options;

        using AscendantEmpireContext context = new(options);

        Assert.DoesNotThrow(() => _ = context.Model.GetEntityTypes().ToList());
    }
}
