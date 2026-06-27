using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Updates an existing tag in the global catalog from the dev Unit Designer's tag manager.
/// </summary>
/// <remarks>
/// The tag is located by its <c>Id</c>; a missing id is a not-found. The <c>Code</c> may be changed,
/// but only to one not already used by another tag (the code is the unique natural key).
/// </remarks>
public record UpdateTagRequest(int Id, UpsertTagDto Tag) : IRequest<ServiceResult<TagView>>;

internal class UpdateTagHandler(
    ILogger<UpdateTagHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<UpdateTagRequest, ServiceResult<TagView>>
{
    public async Task<ServiceResult<TagView>> Handle(UpdateTagRequest request, CancellationToken cancelToken)
    {
        try
        {
            UpsertTagDto dto = request.Tag;

            // Shape validation first, so a malformed request never touches the database.
            List<string> errors = TagValidation.Validate(dto);

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(UpdateTagRequest), string.Join("; ", errors));
                return ServiceResult<TagView>.Fail(errors);
            }

            Tag? tag = await context.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancelToken);

            if (tag is null)
            {
                return ServiceResult<TagView>.NotFound($"No tag exists with id {request.Id}.");
            }

            // If the code is being changed, make sure no *other* tag already uses the new code.
            if (tag.Code != dto.Code)
            {
                bool codeTaken = await context.Tags.AnyAsync(t => t.Code == dto.Code && t.Id != request.Id, cancelToken);

                if (codeTaken)
                {
                    return ServiceResult<TagView>.Conflict($"A tag with code '{dto.Code}' already exists.");
                }
            }

            tag.Code = dto.Code;
            tag.DisplayName = dto.DisplayName;
            tag.Description = dto.Description;
            tag.TagType = dto.TagType;

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Updated tag '{Code}' (id {Id}).", tag.Code, tag.Id);

            return ServiceResult<TagView>.Succeed(ToView(tag));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(UpdateTagHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<TagView>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Projects a persisted <see cref="Tag"/> entity onto the API <see cref="TagView"/> contract.
    /// </summary>
    private static TagView ToView(Tag tag)
    {
        return new TagView
        {
            Id = tag.Id,
            Code = tag.Code,
            DisplayName = tag.DisplayName,
            Description = tag.Description,
            TagType = tag.TagType,
        };
    }
}
