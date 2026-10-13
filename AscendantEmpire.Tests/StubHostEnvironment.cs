using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AscendantEmpire.Tests;

/// <summary>
/// A settable <see cref="IHostEnvironment"/> for code that only reads the environment name.
/// </summary>
/// <remarks>
/// Written out rather than taken from <c>Microsoft.Extensions.Hosting.Internal.HostingEnvironment</c>, which lives in the hosting implementation package
/// rather than its abstractions and is not part of its public contract. The content root points at the test binaries, which is true and unused.
/// </remarks>
/// <param name="environmentName">The name to report, such as <c>Development</c> or <c>Production</c>.</param>
internal sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
{
    /// <summary>
    /// The name whose only common spellings are Development, Staging and Production.
    /// </summary>
    public const string DEVELOPMENT = "Development";

    /// <summary>
    /// The name to use whenever a test is about what a deployed instance would do.
    /// </summary>
    public const string PRODUCTION = "Production";

    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "AscendantEmpire.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
