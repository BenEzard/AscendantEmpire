using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record DeleteMapResourceCategoryRequest(string Code) : IRequest<ServiceResultBase>;

/// <summary>
/// Deletes an empty MapResourceCategory. One with types in it is refused: move or delete the types first.
/// </summary>
internal class DeleteMapResourceCategoryHandler(
    ILogger<DeleteMapResourceCategoryHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<DeleteMapResourceCategoryRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteMapResourceCategoryRequest request, CancellationToken cancellationToken)
    {
        try
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
        }
        catch (Exception ex)
        {
            const string error = "Error deleting the MapResource category.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }

        return ServiceResultBase.SuccessNoContent();
    }
}
