using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the global <c>unitdesigns.UnitTemplate</c> rows every new game spawns for every player — the settler and the scout.
/// </summary>
/// <remarks>
/// <para>
/// Depends on the components: each template names its base body and its fitted components by code, and the assembly resolves those against rows already
/// committed to the database rather than anything in its own payload.
/// </para>
/// <para>
/// Without these two, game creation aborts rather than starting a game with no units — the codes are the ones <c>GameSetupService.StarterTemplateCodes</c> demands. 
/// Templates compile as they are saved, so a seeded template arrives with a live snapshot and needs no separate recompile pass.
/// </para>
/// </remarks>
internal sealed class StarterTemplateSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.StarterTemplates;

    public SeedTargetGroup Group => SeedTargetGroup.UnitDesign;

    public string DisplayName => "Starter Unit Templates";

    public string Description => "The settler and scout templates a new game spawns for every player. Game creation refuses to start without them.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.UnitComponents];

    public int SortOrder => 40;

    public int ItemCount => StarterTemplateSeedData.Build().Templates.Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new BulkUpsertGlobalUnitTemplatesRequest(StarterTemplateSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "starter unit templates", request, cancellationToken);

        return $"{ItemCount} starter unit template(s)";
    }
}
