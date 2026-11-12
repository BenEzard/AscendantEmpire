using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>assets.SupplySet</c> catalogue — what a force eats, what its mounts burn, and what its wounded are patched up with.
/// </summary>
/// <remarks>
/// Runs after <see cref="AssetSeedStep"/>, and not as a convention: a set's members name Assets by code, and against an unseeded catalogue none of them
/// resolves. <see cref="SeedSupplySetsHandler"/> does the writing, since a step has no database access of its own.
/// </remarks>
internal sealed class SupplySetSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.SupplySets;

    public SeedTargetGroup Group => SeedTargetGroup.Assets;

    public string DisplayName => "Supply Sets";

    public string Description => "What a force consumes — human food, fodder for mounts, and the materials its wounded are patched up with.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [SeedTarget.Assets];

    // Behind AssetSeedStep's 10, since every member names an Asset it writes.
    public int SortOrder => 20;

    // The sets, not their members: a set is one upsert however many Assets satisfy it.
    public int ItemCount => SupplySetSeedData.BuildSets().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new SeedSupplySetsRequest();

        await SeedStepDispatch.SendAsync(mediator, "supply sets", request, cancellationToken);

        return $"{ItemCount} supply set(s)";
    }
}
