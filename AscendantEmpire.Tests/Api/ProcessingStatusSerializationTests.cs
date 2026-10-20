using AscendantEmpire.Application.Features.SystemMonitoring.Classes;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AscendantEmpire.Tests.Api;

/// <summary>
/// Pins the wire format of <c>GET /system/status</c>, the response every background-process dialog in the frontend polls.
/// </summary>
/// <remarks>
/// <para>
/// The frontend's copy of this contract is hand-written, so nothing reconciles the two and a mismatch is invisible to a test on either side on its own: a
/// vitest mock built from the TypeScript interface repeats whatever that interface already claims, and a handler test asserts the enum the handler returned
/// rather than the JSON a browser receives. This fixture is the one place that reads the real serializer.
/// </para>
/// <para>
/// The options come out of the running host rather than being rebuilt here, so removing the converter registration in <c>Program.cs</c> fails these tests
/// instead of being faithfully mirrored by a copy of it.
/// </para>
/// <para>
/// No database: nothing here opens a connection, so this sits at the lowest tier <c>docs/testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ProcessingStatusSerializationTests
{
    private ApiTestFactory _factory = null!;
    private JsonSerializerOptions _apiJsonOptions = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new ApiTestFactory();
        _apiJsonOptions = _factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test, Description("Every processing status reaches the browser as its enum name, because the poller reads the terminal states by name and a number would leave a finished job polling forever.")]
    [TestCase(ProcessingStatus.Pending)]
    [TestCase(ProcessingStatus.InProgress)]
    [TestCase(ProcessingStatus.Complete)]
    [TestCase(ProcessingStatus.Failed)]
    public void ProcessingStatusSerializesAsItsName(ProcessingStatus status)
    {
        var dto = new DTProcessingStatus
        {
            ProcessingId = 43,
            ProcessType = ProcessType.DeleteGame,
            Status = status,
            ProgressPercent = 45,
            StartedAt = DateTime.UtcNow
        };

        JsonNode json = Serialize(dto);

        using (Assert.EnterMultipleScope())
        {
            json["status"]!.GetValueKind().Should().Be(JsonValueKind.String);
            json["status"]!.GetValue<string>().Should().Be(status.ToString());
        }
    }

    [Test, Description("A failure reaches the browser with its message intact, since that string is the only account of the failure the dialog can show.")]
    public void AFailedStatusCarriesItsErrorMessage()
    {
        const string message = "The DELETE statement conflicted with the REFERENCE constraint \"FK_ResearchHistory_PlayerTurn_PlayerTurnId\".";

        var dto = new DTProcessingStatus
        {
            ProcessingId = 43,
            ProcessType = ProcessType.DeleteGame,
            Status = ProcessingStatus.Failed,
            ProgressPercent = 45,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = message
        };

        JsonNode json = Serialize(dto);

        json["errorMessage"]!.GetValue<string>().Should().Be(message);
    }

    [Test, Description("Every enum on the status DTO serializes as a string, so a property added later cannot quietly ship as a number that the frontend's comparisons read as a mismatch.")]
    public void EveryEnumOnTheStatusDtoSerializesAsAString()
    {
        PropertyInfo[] enumProperties = typeof(DTProcessingStatus)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => UnderlyingEnumType(property.PropertyType) is not null)
            .ToArray();

        enumProperties.Should().NotBeEmpty("the DTO carries at least Status and ProcessType, so finding none would mean this test is looking in the wrong place.");

        // Populated by reflection rather than by hand so the test covers a property added after it was written. Each one gets its enum's first declared
        // value, because an enum whose numbering starts at 1 - ProcessType does - has no name for the default 0 and would serialize as a bare number
        // whatever converters are registered.
        var dto = new DTProcessingStatus();
        foreach (PropertyInfo property in enumProperties)
        {
            Type enumType = UnderlyingEnumType(property.PropertyType)!;
            property.SetValue(dto, Enum.GetValues(enumType).GetValue(0));
        }

        JsonNode json = Serialize(dto);

        using (Assert.EnterMultipleScope())
        {
            foreach (PropertyInfo property in enumProperties)
            {
                string name = _apiJsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;

                Assert.That(json[name], Is.Not.Null, $"'{property.Name}' is missing from the payload as '{name}'.");
                Assert.That(json[name]!.GetValueKind(), Is.EqualTo(JsonValueKind.String), $"'{name}' should reach the browser as an enum name, not a number.");
            }
        }
    }

    /// <summary>Serializes through the API's own options and hands back the payload as a tree, so a value's JSON kind can be asserted and not just its text.</summary>
    private JsonNode Serialize(DTProcessingStatus dto)
    {
        return JsonNode.Parse(JsonSerializer.Serialize(dto, _apiJsonOptions))!;
    }

    /// <summary>The enum type behind a property, unwrapping a nullable one; null where the property is not an enum at all.</summary>
    private static Type? UnderlyingEnumType(Type propertyType)
    {
        Type candidate = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        return candidate.IsEnum ? candidate : null;
    }
}
