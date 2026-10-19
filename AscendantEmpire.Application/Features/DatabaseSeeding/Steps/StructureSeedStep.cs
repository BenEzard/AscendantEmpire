using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Structures.Handlers;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// Seeds the <c>structure.Structures</c> catalogue — the kinds of Structure that can stand on a tile.
/// </summary>
/// <remarks>
/// Stands alone: a Structure references nothing else the seed writes, and nothing the seed writes references one. It is the settle order and the construction step that need
/// it, neither of which the seed runs.
/// </remarks>
internal sealed class StructureSeedStep : ISeedStep
{
    public SeedTarget Target => SeedTarget.Structures;

    public SeedTargetGroup Group => SeedTargetGroup.Structures;

    public string DisplayName => "Structures";

    public string Description => "The kinds of Structure a tile can hold — the camp and village a settler founds, the watch tower, the palisade and the totem pole.";

    public IReadOnlyCollection<SeedTarget> DependsOn => [];

    public int SortOrder => 10;

    public int ItemCount => StructureSeedData.Build().Count;

    public async Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        var request = new UpsertStructuresRequest(StructureSeedData.Build());

        await SeedStepDispatch.SendAsync(mediator, "structures", request, cancellationToken);

        return $"{ItemCount} structure(s)";
    }
}
