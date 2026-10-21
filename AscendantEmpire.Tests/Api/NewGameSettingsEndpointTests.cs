using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards <c>GET /games/settings</c>, the bounds and defaults the New Game dialog builds its controls from.
/// </summary>
/// <remarks>
/// <para>
/// Asserted against the raw configuration the host loaded rather than against literals, for two reasons. Repeating the shipped numbers here would only
/// pin them to themselves, and reading the JSON directly is independent of the options binding — so a section member that cannot be written to (a
/// property with no setter, say) shows up as a value the endpoint reports as 0 while the configuration says otherwise, rather than as two matching
/// zeroes.
/// </para>
/// <para>
/// No database: the endpoint reads configuration only, so this sits at the lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class NewGameSettingsEndpointTests
{
    private ApiTestFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new ApiTestFactory();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test, Description("Every new-game bound the dialog builds its controls from is reported as configuration spells it, so no section silently reports 0.")]
    public async Task GetGameSettings_ReportsTheConfiguredBounds()
    {
        HttpClient client = _factory.CreateClientWithPermissions();
        IConfiguration configuration = _factory.Services.GetRequiredService<IConfiguration>();

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonNode settings = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        JsonNode battleMapSettings = settings["battleMapSettings"]!;
        JsonNode technologySettings = settings["technologySettings"]!;

        settings["minimumPlayersPerGame"]!.GetValue<int>().Should().Be(configuration.GetValue<int>("GameSettings:MinimumPlayersPerGame"));
        settings["maximumPlayersPerGame"]!.GetValue<int>().Should().Be(configuration.GetValue<int>("GameSettings:MaximumPlayersPerGame"));

        battleMapSettings["minimumBattleMapSize"]!.GetValue<int>()
            .Should().Be(configuration.GetValue<int>("GameSettings:BattleMapSettings:MinimumBattleMapSize"));
        battleMapSettings["maximumBattleMapSize"]!.GetValue<int>()
            .Should().Be(configuration.GetValue<int>("GameSettings:BattleMapSettings:MaximumBattleMapSize"));
        battleMapSettings["defaultBattleMapSize"]!.GetValue<int>()
            .Should().Be(configuration.GetValue<int>("GameSettings:BattleMapSettings:DefaultBattleMapSize"));
        battleMapSettings["battleMapSizeIncrement"]!.GetValue<int>()
            .Should().Be(configuration.GetValue<int>("GameSettings:BattleMapSettings:BattleMapSizeIncrement"));

        technologySettings["baseResearchPoints"]!.GetValue<int>()
            .Should().Be(configuration.GetValue<int>("GameSettings:TechnologySettings:BaseResearchPoints"));
        technologySettings["technologySpeedMultiplierPercent"]!.GetValue<long>()
            .Should().Be(configuration.GetValue<long>("GameSettings:TechnologySettings:TechnologySpeedMultiplierPercent"));
        technologySettings["technologyFuzzinessPercent"]!.GetValue<long>()
            .Should().Be(configuration.GetValue<long>("GameSettings:TechnologySettings:TechnologyFuzzinessPercent"));
    }

    [Test, Description("The default battle map size the dialog opens on is itself a size the save would accept, so the form never starts out invalid.")]
    public async Task GetGameSettings_DefaultBattleMapSize_SitsWithinTheReportedBounds()
    {
        HttpClient client = _factory.CreateClientWithPermissions();

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonNode battleMapSettings = (await response.Content.ReadFromJsonAsync<JsonNode>())!["battleMapSettings"]!;
        int minimum = battleMapSettings["minimumBattleMapSize"]!.GetValue<int>();
        int maximum = battleMapSettings["maximumBattleMapSize"]!.GetValue<int>();

        minimum.Should().BeLessThan(maximum);
        battleMapSettings["defaultBattleMapSize"]!.GetValue<int>().Should().BeInRange(minimum, maximum);
    }

    [Test, Description("The shipped bounds and default all sit on the configured increment, so every notch of the dialog's slider is a size the save accepts.")]
    public async Task GetGameSettings_BattleMapSizeBoundsAndDefault_SitOnTheConfiguredIncrement()
    {
        HttpClient client = _factory.CreateClientWithPermissions();

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonNode battleMapSettings = (await response.Content.ReadFromJsonAsync<JsonNode>())!["battleMapSettings"]!;
        int minimum = battleMapSettings["minimumBattleMapSize"]!.GetValue<int>();
        int maximum = battleMapSettings["maximumBattleMapSize"]!.GetValue<int>();
        int increment = battleMapSettings["battleMapSizeIncrement"]!.GetValue<int>();

        using (new FluentAssertions.Execution.AssertionScope())
        {
            increment.Should().BeGreaterThan(0, "a zero increment would wave the save's divisibility check through entirely");

            // The slider steps from the minimum, so the span is what decides whether its top notch is reachable; the two ends and the opening value
            // all have to be multiples for the knob never to sit on a size the save refuses.
            ((maximum - minimum) % increment).Should().Be(0, "the slider steps from the minimum, so the maximum must be a whole number of steps away");
            (minimum % increment).Should().Be(0);
            (maximum % increment).Should().Be(0);
            (battleMapSettings["defaultBattleMapSize"]!.GetValue<int>() % increment).Should().Be(0);
        }
    }

    [Test, Description("The player bounds leave a usable range, so the dialog's slider is never a single point or inverted.")]
    public async Task GetGameSettings_PlayerBounds_LeaveAUsableRange()
    {
        HttpClient client = _factory.CreateClientWithPermissions();

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonNode settings = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        settings["minimumPlayersPerGame"]!.GetValue<int>().Should().BeGreaterThan(1);
        settings["maximumPlayersPerGame"]!.GetValue<int>()
            .Should().BeGreaterThan(settings["minimumPlayersPerGame"]!.GetValue<int>());
    }

    [Test, Description("The endpoint is behind the fallback policy, so an unauthenticated caller cannot read the server's configuration.")]
    public async Task GetGameSettings_AnonymousCaller_IsRefused()
    {
        HttpClient client = _factory.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync("/games/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
