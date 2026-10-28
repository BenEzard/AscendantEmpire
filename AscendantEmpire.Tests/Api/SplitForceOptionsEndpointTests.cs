using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.BackendContracts.ForceOrders.SplitForce;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Guards the payload of <c>GET /force-orders/actions/split-force/options</c>, which is everything the split dialog knows about the Force it is dividing.
/// </summary>
/// <remarks>
/// <para>
/// Asserted against the JSON on the wire rather than against the returned object, because the two things that can go wrong here are invisible to a test of
/// either side alone: the controller projects the handler's result field by field into its own contract type, where a field nobody copied is silently the
/// type's default, and the enum has to reach the client as its name rather than its number. The handler's own tests cannot see the projection, and the
/// dialog's tests mock the API module, so this is the only place both are visible.
/// </para>
/// <para>
/// No database: the mediator is substituted, so nothing below the controller runs. The lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class SplitForceOptionsEndpointTests
{
    private IMediator _mediator = null!;
    private ApiTestFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _mediator = Substitute.For<IMediator>();

        // Registered last, so it wins over the real mediator the application registers.
        _factory = new ApiTestFactory { ConfigureAdditionalTestServices = services => services.AddSingleton(_mediator) };
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test, Description("Every field the split dialog reads survives the controller's projection and reaches the wire, the leadership model as its name rather than its number.")]
    public async Task GetSplitForceOptions_ReportsEveryFieldTheDialogReads()
    {
        _mediator
            .Send(Arg.Any<GetSplitForceOptionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<SplitForceOptions>.Succeed(new SplitForceOptions
            {
                UnitInstanceId = 55,
                DisplayName = "Settler",
                LeadershipModel = LeadershipModel.Unled,
                HealthyCount = 150,
                InjuredCount = 50,
                TotalStrength = 200,
                BaseSize = 25
            }));

        HttpClient client = _factory.CreateClientWithPermissions();

        HttpResponseMessage response = await client.GetAsync("/force-orders/actions/split-force/options?id=55");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonNode body = (await response.Content.ReadFromJsonAsync<JsonNode>())!;

        using (new FluentAssertions.Execution.AssertionScope())
        {
            body["unitInstanceId"]!.GetValue<int>().Should().Be(55);
            body["displayName"]!.GetValue<string>().Should().Be("Settler");
            // Commissioned is the enum's first member, so a field the projection forgot reads as "Commissioned" rather than as anything obviously unset.
            body["leadershipModel"]!.GetValue<string>().Should().Be("Unled");
            body["healthyCount"]!.GetValue<int>().Should().Be(150);
            body["injuredCount"]!.GetValue<int>().Should().Be(50);
            body["totalStrength"]!.GetValue<int>().Should().Be(200);
            body["baseSize"]!.GetValue<int>().Should().Be(25);
        }
    }
}
