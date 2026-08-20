using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Covers <see cref="UpsertTagTypesHandler"/>: the duplicate-key guard, both branches of the upsert, and
/// the up-front validation that is meant to reject a batch before anything reaches the database.
/// </summary>
/// <remarks>
/// <para>
/// The Tag Type Setup dialog always posts the <em>whole</em> edited set rather than a delta, so a single
/// request routinely mixes rows that already exist with rows that do not. That is why the insert and
/// update branches are exercised together in one save rather than in isolation.
/// </para>
/// <para>
/// Tier 2 (in-memory). <c>TagTypeInfo</c> is a flat entity with no complex types, and the handler does
/// plain EF reads and writes with no raw SQL or transactions, so a container buys nothing here. Same tier
/// as <see cref="TagHandlersTests"/> — see <c>docs/testing.md</c>.
/// </para>
/// </remarks>
internal class TagTypeHandlersTests : InMemoryTestBase
{
    /// <summary>
    /// Builds a valid tag type upsert. Every field is given a distinct value so an update asserting on
    /// them cannot pass by coincidence.
    /// </summary>
    private static UpsertTagTypeDto BuildDto(
        TagType tagTypeId,
        string displayName = "Display Name",
        string description = "Description",
        bool isInherited = false,
        string? colour = "#3366cc",
        string? icon = "shield")
    {
        return new UpsertTagTypeDto
        {
            TagTypeId = tagTypeId,
            DisplayName = displayName,
            Description = description,
            IsInherited = isInherited,
            Colour = colour,
            Icon = icon,
        };
    }

    /// <summary>
    /// Runs the upsert handler against a fresh context and returns its result.
    /// </summary>
    private async Task<ServiceResultBase> UpsertAsync(params UpsertTagTypeDto[] tagTypes)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new UpsertTagTypesHandler(new NullLogger<UpsertTagTypesHandler>(), context);
        return await handler.Handle(new UpsertTagTypesRequest(tagTypes), CancellationToken.None);
    }

    /// <summary>
    /// Writes a tag type row directly, so a test can arrange an "already exists" state without going
    /// through the handler it is about to exercise.
    /// </summary>
    private async Task SeedAsync(TagType tagTypeId, string displayName)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TagTypeInfos.Add(new TagTypeInfo
        {
            TagTypeId = tagTypeId,
            DisplayName = displayName,
            Description = "Seeded",
            IsInherited = false,
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("A batch naming the same tag type twice is rejected, because the upsert would otherwise be ambiguous.")]
    public async Task DuplicateTagTypeKeyIsRejectedAsync()
    {
        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, displayName: "First"),
            BuildDto(TagType.Combat, displayName: "Second"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int rowCount = await verifyContext.TagTypeInfos.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("specified multiple times"));
            Assert.That(rowCount, Is.Zero, "A rejected batch writes nothing.");
        }
    }

    [Test, Description("A batch mixing a known and an unknown tag type updates the one and inserts the other, which is how the setup dialog always calls it.")]
    public async Task MissingTagTypeRowIsInsertedAsync()
    {
        await SeedAsync(TagType.Combat, "Original Combat");

        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, displayName: "Edited Combat"),
            BuildDto(TagType.Ability, displayName: "New Ability"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        List<TagTypeInfo> rows = await verifyContext.TagTypeInfos.ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
            Assert.That(rows, Has.Count.EqualTo(2), "The known row was updated rather than duplicated, and the unknown one added.");
            Assert.That(rows.Single(row => row.TagTypeId == TagType.Combat).DisplayName, Is.EqualTo("Edited Combat"));
            Assert.That(rows.Single(row => row.TagTypeId == TagType.Ability).DisplayName, Is.EqualTo("New Ability"));
        }
    }

    [Test, Description("Upserting an existing tag type overwrites all five editable fields on the same row.")]
    public async Task ExistingTagTypeIsUpdatedInPlaceAsync()
    {
        await SeedAsync(TagType.Movement, "Original Movement");

        ServiceResultBase result = await UpsertAsync(BuildDto(
            TagType.Movement,
            displayName: "Edited Movement",
            description: "Edited description",
            isInherited: true,
            colour: "#ff0000",
            icon: "boot"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        List<TagTypeInfo> rows = await verifyContext.TagTypeInfos.ToListAsync();
        TagTypeInfo updated = rows.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(rows, Has.Count.EqualTo(1), "The row is updated in place — the enum key means there can only ever be one.");
            Assert.That(updated.DisplayName, Is.EqualTo("Edited Movement"));
            Assert.That(updated.Description, Is.EqualTo("Edited description"));
            Assert.That(updated.IsInherited, Is.True);
            Assert.That(updated.Colour, Is.EqualTo("#ff0000"));
            Assert.That(updated.Icon, Is.EqualTo("boot"));
        }
    }

    [Test, Description("One invalid row fails the whole batch and writes nothing — including the rows that were valid.")]
    public async Task InvalidTagTypeWritesNothingAsync()
    {
        // The handler validates every row before touching the database, so the valid row alongside this
        // one must not be persisted. A partially-applied batch is the failure mode worth pinning: the
        // dialog posts the entire set, so a half-save would leave the catalogue silently inconsistent.
        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Classification, displayName: "Perfectly Valid"),
            BuildDto(TagType.Ability, displayName: "   "));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int rowCount = await verifyContext.TagTypeInfos.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("missing its DisplayName"));
            Assert.That(rowCount, Is.Zero, "The valid row in the same batch is not persisted either.");
        }
    }
}
