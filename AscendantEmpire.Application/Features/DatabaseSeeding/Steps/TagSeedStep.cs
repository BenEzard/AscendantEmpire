using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>unitdesigns.Tag</c> catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Depends on the tag types: <c>Tag.TagType</c> is a foreign key into the tag-type table (<c>FK_Tag_TagType_TagType</c>, restricted), so a tag written
/// against a tag type that does not exist fails at the database rather than in validation.
/// </para>
/// <para>
/// Goes through <see cref="BulkUpsertTagsRequest"/> rather than riding inside the component payload, which is what makes the tag catalogue refreshable on
/// its own. This step is now the only writer of the seeded tags — <see cref="UnitComponentSeedData"/> names them by code and no longer carries their
/// definitions, so the components step depends on this one having run.
/// </para>
/// </remarks>
internal sealed class TagSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Tags;

    public SeedTargetGroup Group => SeedTargetGroup.UnitDesign;

    public string DisplayName => "Tags";

    public string Description => "The tag catalogue, including the settler, scout and infantry organisation tags a new game's starting units depend on.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.TagTypes];

    public int SortOrder => 20;

    public int ItemCount => TagSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new BulkUpsertTagsRequest(TagSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "tags", request, cancellationToken);

        return $"{ItemCount} tag(s)";
    }
}
