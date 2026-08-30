using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Tags;

/// <summary>
/// Covers <see cref="UpsertTagTypesHandler"/>: the duplicate-key guard, the colour-uniqueness rule, both
/// branches of the upsert, and the up-front validation meant to reject a batch before anything reaches the
/// database.
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
    /// <remarks>
    /// Colour defaults to <c>null</c> rather than a literal: colours must be unique across the catalogue, so a
    /// shared default would make every multi-row batch collide. Tests that care about colour pass their own.
    /// </remarks>
    private static UpsertTagTypeDto BuildDto(
        TagType tagTypeId,
        string displayName = "Display Name",
        string description = "Description",
        bool isInheritedToBase = false,
        string? colour = null,
        string? icon = "shield")
    {
        return new UpsertTagTypeDto
        {
            TagTypeId = tagTypeId,
            DisplayName = displayName,
            Description = description,
            IsInheritedToBase = isInheritedToBase,
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
    private async Task SeedAsync(TagType tagTypeId, string displayName, string? colour = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.TagTypeInfos.Add(new TagTypeInfo
        {
            TagTypeId = tagTypeId,
            DisplayName = displayName,
            Description = "Seeded",
            IsInheritedToBase = false,
            Colour = colour,
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
            isInheritedToBase: true,
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
            Assert.That(updated.IsInheritedToBase, Is.True);
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

    [Test, Description("Two tag types in one batch cannot claim the same colour, since a colour is how a tag type is recognised at a glance.")]
    public async Task DuplicateColourWithinBatchIsRejectedAsync()
    {
        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, colour: "#3366cc"),
            BuildDto(TagType.Ability, colour: "#3366cc"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int rowCount = await verifyContext.TagTypeInfos.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("used by more than one tag type"));
            Assert.That(rowCount, Is.Zero, "A rejected batch writes nothing.");
        }
    }

    [Test, Description("Colour clashes are caught regardless of casing or surrounding whitespace, so '#FF0000' cannot slip past '#ff0000'.")]
    public async Task DuplicateColourIsMatchedIgnoringCaseAndWhitespaceAsync()
    {
        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, colour: "#FF0000"),
            BuildDto(TagType.Ability, colour: "  #ff0000  "));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("used by more than one tag type"));
        }
    }

    [Test, Description("A colour held by a tag type the request never mentions is still taken, so a partial save cannot hand it out twice.")]
    public async Task ColourAlreadyHeldByAnUntouchedRowIsRejectedAsync()
    {
        await SeedAsync(TagType.Movement, "Movement", colour: "#3366cc");

        // The batch says nothing about Movement, so only the database knows that colour is spoken for.
        ServiceResultBase result = await UpsertAsync(BuildDto(TagType.Combat, colour: "#3366cc"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        List<TagTypeInfo> rows = await verifyContext.TagTypeInfos.ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("already used by tag type 'Movement'"));
            Assert.That(rows, Has.Count.EqualTo(1), "The rejected insert never happened.");
        }
    }

    [Test, Description("A tag type keeps its own colour on re-save — the row being updated must not be treated as its own clash.")]
    public async Task RowKeepingItsOwnColourIsAcceptedAsync()
    {
        await SeedAsync(TagType.Movement, "Movement", colour: "#3366cc");

        ServiceResultBase result = await UpsertAsync(BuildDto(TagType.Movement, displayName: "Edited Movement", colour: "#3366cc"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        TagTypeInfo updated = await verifyContext.TagTypeInfos.SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(updated.DisplayName, Is.EqualTo("Edited Movement"));
            Assert.That(updated.Colour, Is.EqualTo("#3366cc"));
        }
    }

    [Test, Description("Two tag types may swap colours in one batch, because the whole set is posted together rather than one row at a time.")]
    public async Task ColoursMaySwapBetweenRowsInOneBatchAsync()
    {
        await SeedAsync(TagType.Combat, "Combat", colour: "#ff0000");
        await SeedAsync(TagType.Ability, "Ability", colour: "#00ff00");

        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, colour: "#00ff00"),
            BuildDto(TagType.Ability, colour: "#ff0000"));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        List<TagTypeInfo> rows = await verifyContext.TagTypeInfos.ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(rows.Single(row => row.TagTypeId == TagType.Combat).Colour, Is.EqualTo("#00ff00"));
            Assert.That(rows.Single(row => row.TagTypeId == TagType.Ability).Colour, Is.EqualTo("#ff0000"));
        }
    }

    [Test, Description("Any number of tag types may go uncoloured — the uniqueness rule only applies to colours actually set.")]
    public async Task MultipleUncolouredRowsAreAcceptedAsync()
    {
        ServiceResultBase result = await UpsertAsync(
            BuildDto(TagType.Combat, colour: null),
            BuildDto(TagType.Ability, colour: null),
            BuildDto(TagType.Movement, colour: "   "));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        int rowCount = await verifyContext.TagTypeInfos.CountAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(rowCount, Is.EqualTo(3));
        }
    }
}
