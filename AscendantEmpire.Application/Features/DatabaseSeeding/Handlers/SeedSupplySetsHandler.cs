using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;

/// <summary>
/// Seeds the supply sets, matching each against what the catalogue already holds so a re-run edits rather than duplicates.
/// </summary>
/// <remarks>
/// <para>
/// The sets themselves are enumerated in <see cref="SupplySetSeedData"/>. What needs the database is the match: a set is identified by its code, while the
/// upsert handlers take an id, so the existing ids are read back by code first. The finished sets then go through the same create/update handlers the
/// Entity Setup Tool's dialog uses, which is what keeps seeding and hand-editing from diverging on how a member is resolved.
/// </para>
/// <para>
/// Depends on the <b>Assets</b> target having run: a member names an Asset by code, and an empty catalogue resolves none of them. That is reported here by
/// name, because the upsert's "Asset code(s) that do not resolve" would not say which seed was missing.
/// </para>
/// </remarks>
public record SeedSupplySetsRequest() : IRequest<ServiceResultBase>;

internal class SeedSupplySetsHandler(
    AscendantEmpireContext context,
    IMediator mediator
) : IRequestHandler<SeedSupplySetsRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SeedSupplySetsRequest request, CancellationToken cancellationToken)
    {
        bool catalogueIsEmpty = !await context.Assets.AnyAsync(cancellationToken);

        if (catalogueIsEmpty)
        {
            return Fail("The Asset catalogue is empty, so no supply set member resolves. Run the Assets database seed from the Entity Setup Tool first.");
        }

        IReadOnlyCollection<UpsertSupplySetDto> sets = SupplySetSeedData.BuildSets();

        // Codes are the seed's match key, so a re-run edits the set it wrote last time instead of tripping the unique index.
        string[] codes = [.. sets.Select(set => set.Code)];

        Dictionary<string, int> existingIdsByCode = await context.SupplySets
            .AsNoTracking()
            .Where(set => codes.Contains(set.Code))
            .ToDictionaryAsync(set => set.Code, set => set.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (UpsertSupplySetDto set in sets)
        {
            ServiceResult<SupplySetView> result = existingIdsByCode.TryGetValue(set.Code, out int existingId)
                ? await mediator.Send(new UpdateSupplySetRequest(existingId, set), cancellationToken)
                : await mediator.Send(new CreateSupplySetRequest(set), cancellationToken);

            if (!result.HasSucceeded)
            {
                return Fail($"Seeding supply set '{set.Code}' failed: {string.Join("; ", result.Errors)}");
            }
        }

        return SuccessNoContent();
    }
}
