using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Integration tests for the global Tag CRUD endpoints, focused on the <c>SystemCreated</c> provenance
/// flag: it defaults to false on create, an explicit value round-trips through create/get, and the
/// update endpoint can change it.
/// </summary>
/// <remarks>
/// <c>Tag</c> is a flat entity and the handlers only do plain EF reads/writes, so the in-memory tier suffices.
/// </remarks>
internal class TagHandlersTests : InMemoryTestBase
{
    /// <summary>
    /// Builds a minimal valid tag upsert (Code, DisplayName and TagType are the only required fields).
    /// </summary>
    private static UpsertTagDto BuildDto(string code, bool systemCreated = false)
    {
        return new UpsertTagDto
        {
            Code = code,
            DisplayName = code,
            TagType = TagType.Ability,
            SystemCreated = systemCreated,
        };
    }

    /// <summary>
    /// Runs the create handler against a fresh context and returns its result.
    /// </summary>
    private async Task<ServiceResult<TagView>> CreateAsync(UpsertTagDto dto)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new CreateTagHandler(new NullLogger<CreateTagHandler>(), context);
        return await handler.Handle(new CreateTagRequest(dto), CancellationToken.None);
    }

    [Test, Description("A created tag defaults SystemCreated to false when the DTO leaves it unset.")]
    public async Task CreateDefaultsSystemCreatedToFalseAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(BuildDto("plain"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
            Assert.That(result.SuccessValue!.SystemCreated, Is.False);
        }
    }

    [Test, Description("An explicit SystemCreated=true round-trips through create and the get endpoint.")]
    public async Task CreateWithSystemCreatedTrueRoundTripsAsync()
    {
        ServiceResult<TagView> created = await CreateAsync(BuildDto("builtin", systemCreated: true));
        Assert.That(created.SuccessValue!.SystemCreated, Is.True, "Create should echo the flag back.");

        // Re-read via the get endpoint to prove the flag was actually persisted, not just projected.
        var getHandler = new GetTagsHandler(new NullLogger<GetTagsHandler>(), DbFactory);
        ServiceResult<GetTagsResult> fetched = await getHandler.Handle(new GetTagsRequest(), CancellationToken.None);

        TagView tag = fetched.SuccessValue!.Tags.Single(t => t.Code == "builtin");
        Assert.That(tag.SystemCreated, Is.True);
    }

    [Test, Description("Updating a tag can change its SystemCreated flag.")]
    public async Task UpdateChangesSystemCreatedAsync()
    {
        ServiceResult<TagView> created = await CreateAsync(BuildDto("promote", systemCreated: false));
        int id = created.SuccessValue!.Id;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var updateHandler = new UpdateTagHandler(new NullLogger<UpdateTagHandler>(), context);
        ServiceResult<TagView> updated = await updateHandler.Handle(
            new UpdateTagRequest(id, BuildDto("promote", systemCreated: true)),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(updated.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(updated.SuccessValue!.SystemCreated, Is.True);
        }
    }
}
