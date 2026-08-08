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

    /// <summary>
    /// Builds an organisation-group tag upsert with a leadership model and (optionally) the unled sizes.
    /// </summary>
    private static UpsertTagDto BuildOrganisationDto(
        string code,
        LeadershipModel? leadershipModel,
        int? baseSize = null,
        int? defaultSize = null,
        int? maxSize = null)
    {
        return new UpsertTagDto
        {
            Code = code,
            DisplayName = code,
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = leadershipModel,
            UnledBaseSize = baseSize,
            UnledDefaultSize = defaultSize,
            UnledMaxSize = maxSize,
        };
    }

    [Test, Description("A valid unled organisation tag persists its leadership model and all three sizes.")]
    public async Task CreateUnledTagRoundTripsSizingAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(
            BuildOrganisationDto("settler", LeadershipModel.Unled, baseSize: 25, defaultSize: 200, maxSize: 5000));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Created));
            Assert.That(result.SuccessValue!.LeadershipModel, Is.EqualTo(LeadershipModel.Unled));
            Assert.That(result.SuccessValue.UnledBaseSize, Is.EqualTo(25));
            Assert.That(result.SuccessValue.UnledDefaultSize, Is.EqualTo(200));
            Assert.That(result.SuccessValue.UnledMaxSize, Is.EqualTo(5000));
        }
    }

    [Test, Description("An unled tag missing any of its three sizes is rejected.")]
    public async Task CreateUnledTagWithoutSizesFailsAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(
            BuildOrganisationDto("half-sized", LeadershipModel.Unled, baseSize: 25));

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("Unled sizes must satisfy base <= default <= max.")]
    public async Task CreateUnledTagWithDisorderedSizesFailsAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(
            BuildOrganisationDto("disordered", LeadershipModel.Unled, baseSize: 50, defaultSize: 25, maxSize: 100));

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("The unled default size must be a whole multiple of the base quantum.")]
    public async Task CreateUnledTagWithNonMultipleDefaultFailsAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(
            BuildOrganisationDto("ragged", LeadershipModel.Unled, baseSize: 25, defaultSize: 90, maxSize: 200));

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("Unled sizes are rejected on a commissioned organisation tag.")]
    public async Task CreateCommissionedTagWithUnledSizesFailsAsync()
    {
        ServiceResult<TagView> result = await CreateAsync(
            BuildOrganisationDto("infantry", LeadershipModel.Commissioned, baseSize: 25, defaultSize: 200, maxSize: 5000));

        Assert.That(result.HasSucceeded, Is.False);
    }

    [Test, Description("A leadership model is rejected on a non-organisation tag type.")]
    public async Task CreateNonOrganisationTagWithLeadershipModelFailsAsync()
    {
        UpsertTagDto dto = new UpsertTagDto
        {
            Code = "sneaky",
            DisplayName = "Sneaky",
            TagType = TagType.Ability,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 1,
            UnledDefaultSize = 1,
            UnledMaxSize = 1,
        };

        ServiceResult<TagView> result = await CreateAsync(dto);

        Assert.That(result.HasSucceeded, Is.False);
    }
}
