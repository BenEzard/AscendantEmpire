using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>technology.ExperienceType</c> catalogue — the kinds of experience an action can earn.
/// </summary>
/// <remarks>
/// Stands alone: the catalogue references nothing else, and it is what the technologies point at rather than the other way round. Must run before
/// <see cref="TechnologySeedStep"/>, which names these codes.
/// </remarks>
internal sealed class ExperienceTypeSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.ExperienceTypes;

    public SeedTargetGroup Group => SeedTargetGroup.Technology;

    public string DisplayName => "Experience Types";

    public string Description => "The kinds of experience actions earn — biomes, extraction, refining, manufacture, husbandry and exploration.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 10;

    public int ItemCount => ExperienceTypeSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "experience types", request, cancellationToken);

        return $"{ItemCount} experience type(s)";
    }
}
