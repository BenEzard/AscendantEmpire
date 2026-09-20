using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests the <c>MetadataJson</c> bag that carries a seed job's target list from the initiating handler to the executor.
/// </summary>
/// <remarks>
/// No database: this is plain JSON round-tripping. It is worth pinning because the two ends of the job agree on a property name and nothing else would catch
/// a mismatch until a seed silently ran the wrong thing — the executor reads an unreadable bag as "seed everything".
/// </remarks>
[TestFixture]
internal class DatabaseSeedMetadataTests
{
    [Test, Description("A written target list reads back as the same targets in the same order, so the executor runs exactly what the handler resolved.")]
    public void ShouldRoundTripTheSelectedTargets()
    {
        SeedTarget[] targets = [SeedTarget.TagTypes, SeedTarget.Tags, SeedTarget.UnitComponents];

        IReadOnlyCollection<SeedTarget>? read = DatabaseSeedMetadata.TryRead(DatabaseSeedMetadata.Write(targets));

        Assert.That(read, Is.EqualTo(targets));
    }

    [Test, Description("Targets are written as their enum names, so a Processing row stays readable and survives a renumbering of the enum.")]
    public void ShouldWriteTargetsAsNames()
    {
        string metadataJson = DatabaseSeedMetadata.Write([SeedTarget.Biomes]);

        Assert.That(metadataJson, Does.Contain(nameof(SeedTarget.Biomes)));
    }

    [Test, Description("A name the enum no longer has makes the whole bag unreadable, so a job written by an older build seeds everything rather than a silent subset.")]
    public void ShouldRejectAnUnknownTargetName()
    {
        Assert.That(DatabaseSeedMetadata.TryRead("{\"targets\":[\"Biomes\",\"Retired\"]}"), Is.Null);
    }

    [TestCase(null, Description = "Null metadata is unreadable — a job queued before the selection existed carries none.")]
    [TestCase("", Description = "Empty metadata is unreadable rather than an empty selection.")]
    [TestCase("   ", Description = "Whitespace-only metadata is unreadable rather than an empty selection.")]
    [TestCase("not json", Description = "Malformed JSON is unreadable rather than throwing out of the executor.")]
    [TestCase("{\"gameId\":4}", Description = "Metadata from another process type carries no targets.")]
    [TestCase("{\"targets\":\"Biomes\"}", Description = "A targets property that is not an array is unreadable.")]
    [TestCase("{\"targets\":[]}", Description = "An empty target array is unreadable rather than a seed that writes nothing.")]
    [TestCase("{\"targets\":[7]}", Description = "Targets written as numbers are unreadable; only names are accepted.")]
    public void ShouldReturnNullForMetadataItCannotRead(string? metadataJson)
    {
        Assert.That(DatabaseSeedMetadata.TryRead(metadataJson), Is.Null);
    }
}
