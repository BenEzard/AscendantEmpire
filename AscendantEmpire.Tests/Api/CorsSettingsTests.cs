using AscendantEmpire.Api.Options;

using FluentAssertions;

using Microsoft.Extensions.Configuration;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards <see cref="CorsSettings"/>, the configured origin list that replaced the hard-coded <c>WithOrigins("http://localhost:5173")</c> in the host.
/// </summary>
/// <remarks>
/// <para>
/// Worth pinning rather than trusting, because a mistake in this list is invisible from the server's side: the API answers the request normally and the
/// browser discards the response, so the only symptom is a frontend that cannot reach an API that looks healthy. Every rejection below is a value that
/// would produce exactly that, which is why the settings class refuses it at startup instead.
/// </para>
/// <para>
/// No host and no database: this is the settings object and the configuration binder on their own, the lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class CorsSettingsTests
{
    [Test, Description("Every configured origin survives validation in order and trimmed, so stray whitespace in a settings file cannot stop a match.")]
    public void GetValidatedOrigins_ReturnsEveryConfiguredOriginTrimmed()
    {
        CorsSettings settings = new()
        {
            AllowedOrigins = ["  http://localhost:5173  ", "https://play.example.com"],
        };

        settings.GetValidatedOrigins().Should().Equal("http://localhost:5173", "https://play.example.com");
    }

    [Test, Description("A blank entry is dropped rather than rejected, so a hole in an indexed environment variable does not stop the host booting.")]
    public void GetValidatedOrigins_DropsBlankEntries()
    {
        CorsSettings settings = new()
        {
            AllowedOrigins = ["", "   ", "https://play.example.com"],
        };

        settings.GetValidatedOrigins().Should().Equal("https://play.example.com");
    }

    [Test, Description("An absent or empty list fails at startup, rather than leaving a running API no browser can call.")]
    public void GetValidatedOrigins_WithNoOrigins_Throws()
    {
        CorsSettings settings = new();

        settings.Invoking(candidate => candidate.GetValidatedOrigins())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Cors:AllowedOrigins*");
    }

    [Test, Description("A list of nothing but blanks fails the same way an empty one does, since dropping them leaves no origin either.")]
    public void GetValidatedOrigins_WithOnlyBlankOrigins_Throws()
    {
        CorsSettings settings = new()
        {
            AllowedOrigins = ["", "  "],
        };

        settings.Invoking(candidate => candidate.GetValidatedOrigins())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Cors:AllowedOrigins*");
    }

    [TestCase("http://localhost:5173/", Description = "A trailing slash is the common mistake and never matches an Origin header, which carries none.")]
    [TestCase("https://play.example.com/app", Description = "An Origin header carries no path, so a path here could not match any request.")]
    [TestCase("https://play.example.com?x=1", Description = "An Origin header carries no query string either.")]
    [TestCase("https://play.example.com#top", Description = "An Origin header carries no fragment either.")]
    [TestCase("https://user:pw@play.example.com", Description = "An Origin header carries no credentials, so user info could not match.")]
    [TestCase("*", Description = "ASP.NET reads a lone '*' as any-origin, which should be a reviewed code change rather than a settings value.")]
    [TestCase("play.example.com", Description = "A bare host is not an origin: without a scheme there is nothing to compare against.")]
    [TestCase("ftp://play.example.com", Description = "A browser only ever sends an http or https origin, so any other scheme is dead configuration.")]
    public void GetValidatedOrigins_WithAnythingOtherThanABareOrigin_Throws(string configuredOrigin)
    {
        CorsSettings settings = new()
        {
            AllowedOrigins = [configuredOrigin],
        };

        settings.Invoking(candidate => candidate.GetValidatedOrigins())
            .Should().Throw<InvalidOperationException>()
            // The offending value belongs in the message: with several origins configured, "one of them is wrong" is not enough to act on.
            .WithMessage($"*{configuredOrigin}*");
    }

    [Test, Description("The origin list binds from a JSON-shaped configuration array, so an init-only string[] cannot silently arrive empty.")]
    public void AllowedOrigins_BindsFromAnIndexedConfigurationSection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["Cors:AllowedOrigins:1"] = "https://play.example.com",
            })
            .Build();

        CorsSettings? settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();

        settings.Should().NotBeNull();
        settings!.GetValidatedOrigins().Should().Equal("http://localhost:5173", "https://play.example.com");
    }

    [Test, Description("A missing section binds to null, which is why the host falls back to a default instance rather than dereferencing the result.")]
    public void AllowedOrigins_WithNoSection_BindsToNull()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>().Should().BeNull();
    }
}
