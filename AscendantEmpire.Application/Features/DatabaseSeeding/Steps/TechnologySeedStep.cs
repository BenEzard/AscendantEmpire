using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>technology.Technology</c> catalogue, with its prerequisites and experience mappings.
/// </summary>
/// <remarks>
/// <para>
/// Depends on <see cref="SeedTarget.ExperienceTypes"/> only, because the seeded technologies name experience types and nothing else — the catalogue's
/// grant annotations are carried as description text rather than as resolvable targets. <b>When grants or acquisition costs are added to the seed data,
/// this step's dependencies have to grow with them</b> — Assets for costs and for Asset grants, UnitComponents and StarterTemplates for the
/// unit-design ones — or the upsert will reject the whole batch for an unresolvable target.
/// </para>
/// <para>
/// Runs through the same upsert the Entity Setup Tool's editor does, so seeding and hand-editing cannot diverge in how a row is matched or written.
/// </para>
/// </remarks>
internal sealed class TechnologySeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Technologies;

    public SeedTargetGroup Group => SeedTargetGroup.Technology;

    public string DisplayName => "Technologies";

    public string Description => "The technology tree — every technology with its prerequisites and experience sources.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.ExperienceTypes];

    public int SortOrder => 20;

    public int ItemCount => TechnologySeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertTechnologiesRequest(TechnologySeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "technologies", request, cancellationToken);

        return $"{ItemCount} technolog(ies)";
    }
}
