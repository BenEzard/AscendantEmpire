using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record DeleteMapResourceCategoryRequest(string Code) : IRequest<ServiceResultBase>;

/// <summary>
/// Deletes an empty MapResourceCategory. One with types in it is refused: move or delete the types first.
/// </summary>
internal class DeleteMapResourceCategoryHandler(
    AscendantEmpireContext context
) : IRequestHandler<DeleteMapResourceCategoryRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteMapResourceCategoryRequest request, CancellationToken cancellationToken)
    {
        MapResourceCategory? category = await context.MapResourceCategories.SingleOrDefaultAsync(c => c.Code == request.Code, cancellationToken);

        if (category == null)
        {
            return ServiceResultBase.NotFound($"There is no MapResource category '{request.Code}'.");
        }

        int types = await context.MapResourceTypes.CountAsync(t => t.MapResourceCategoryId == category.Id, cancellationToken);

        if (types != 0)
        {
            return ServiceResultBase.Fail($"Category {category.Code} cannot be deleted: it still has {types} MapResourceType(s).");
        }

        context.MapResourceCategories.Remove(category);
        await context.SaveChangesAsync(CancellationToken.None);

        return ServiceResultBase.SuccessNoContent();
    }
}
