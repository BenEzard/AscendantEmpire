using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>unitdesigns.TagType</c> metadata catalogue, one row per <see cref="Domain.Enums.UnitDesigns.TagType"/> member.
/// </summary>
/// <remarks>
/// The root of the unit-design chain. These rows used to be an EF <c>HasData</c> block, so a database migrated but never seeded has none of them — and
/// <c>Tag.TagType</c> is a foreign key into this table, which is why every tag ultimately waits on this step.
/// </remarks>
internal sealed class TagTypeSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.TagTypes;

    public SeedTargetGroup Group => SeedTargetGroup.UnitDesign;

    public string DisplayName => "Tag Types";

    public string Description => "The tag classifications tags are filed under — Classification, Combat, Movement, Ability and Military Organisational Group.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 10;

    public int ItemCount => TagTypeSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertTagTypesRequest(TagTypeSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "tag types", request, cancellationToken);

        return $"{ItemCount} tag type(s)";
    }
}
