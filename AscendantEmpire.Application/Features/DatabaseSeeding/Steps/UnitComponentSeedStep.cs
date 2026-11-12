using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>unitdesigns.UnitComponent</c> catalogue — the human body, the equipment and training fitted to it, and the primitive weapons its slots expect.
/// </summary>
/// <remarks>
/// <para>
/// Depends on the tags: each component names the tags it carries by code, and the bulk-upsert rejects a component whose tag code resolves to nothing in the
/// payload or the database. It depends on the Assets for the same reason, since a weapon names the ammunition it accepts by Asset code.
/// </para>
/// <para>
/// Not inert. The bulk-upsert recompiles every saved template built on the components it touches, so running this step alone still refreshes the snapshots
/// of hand-authored templates — which is the point, since a changed component should propagate.
/// </para>
/// </remarks>
internal sealed class UnitComponentSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.UnitComponents;

    public SeedTargetGroup Group => SeedTargetGroup.UnitDesign;

    public string DisplayName => "Unit Components";

    public string Description => "The human body and its slots, the camping materials and recon training.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.Tags, SeedTarget.SupplySets, SeedTarget.Assets];

    public int SortOrder => 30;

    public int ItemCount => UnitComponentSeedData.Build().Components.Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new BulkUpsertUnitComponentsRequest(UnitComponentSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "unit components", request, cancellationToken);

        return $"{ItemCount} unit component(s)";
    }
}
