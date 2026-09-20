using AscendantEmpire.Domain.Enums.System;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Classes;

/// <summary>
/// The <c>MetadataJson</c> bag carried by a <c>SeedDatabase</c> background job — which parts of the database the job is to seed.
/// </summary>
/// <remarks>
/// <para>
/// A <c>Processing</c> row has no foreign key to anything, so the selection has to travel as loose JSON. Reading and writing it lives here rather than being
/// spelled out at each end, because the initiating handler and the executor must agree on the property name exactly and nothing else would catch a mismatch
/// until the job failed at run time.
/// </para>
/// <para>
/// The handler writes the selection <i>after</i> expanding it over its prerequisites, so the row records what the job really did rather than what was ticked.
/// </para>
/// </remarks>
internal static class DatabaseSeedMetadata
{
    /// <summary>The JSON property the selected targets are stored under.</summary>
    private const string _TARGETS_PROPERTY = "targets";

    /// <summary>
    /// Targets are written as their enum names, so a row stays readable in the database and survives a renumbering of the enum.
    /// </summary>
    private static readonly JsonSerializerOptions _SERIALIZER_OPTIONS = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Builds the <c>MetadataJson</c> value for a seed job covering <paramref name="targets"/>.
    /// </summary>
    public static string Write(IReadOnlyCollection<SeedTarget> targets)
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, IReadOnlyCollection<SeedTarget>> { [_TARGETS_PROPERTY] = targets },
            _SERIALIZER_OPTIONS);
    }

    /// <summary>
    /// Reads the selected targets back out of a <c>MetadataJson</c> value.
    /// </summary>
    /// <returns>The targets, or null when the metadata is missing, unparseable, or does not carry any.</returns>
    /// <remarks>
    /// Returns null rather than throwing so the caller can decide what a row it cannot read means. The executor treats null as "seed everything", which is
    /// what a job queued before the selection existed meant, and is the only reading that cannot silently under-seed.
    /// </remarks>
    public static IReadOnlyCollection<SeedTarget>? TryRead(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(metadataJson);

            if (document.RootElement.TryGetProperty(_TARGETS_PROPERTY, out JsonElement targetsElement) == false)
            {
                return null;
            }

            if (targetsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            List<SeedTarget> targets = [];

            foreach (JsonElement element in targetsElement.EnumerateArray())
            {
                // A name the enum no longer has means the row was written by an older build; treat the whole
                // bag as unreadable rather than silently seeding a subset of what was asked for.
                if (element.ValueKind != JsonValueKind.String
                    || Enum.TryParse(element.GetString(), ignoreCase: true, out SeedTarget target) == false
                    || Enum.IsDefined(target) == false)
                {
                    return null;
                }

                targets.Add(target);
            }

            return targets.Count > 0 ? targets : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
