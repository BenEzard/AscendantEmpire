using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Creates a single Tag in the global catalogue from the dev Unit Designer's tag manager.
/// </summary>
/// <remarks>
/// The tag's <c>Code</c> is the unique natural key, so a request whose code already exists is rejected
/// as a conflict rather than silently updated (use the update endpoint to change an existing tag).
/// </remarks>
public record CreateTagRequest(UpsertTagDto Tag) : IRequest<ServiceResult<TagView>>;

internal class CreateTagHandler(
    ILogger<CreateTagHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<CreateTagRequest, ServiceResult<TagView>>
{
    public async Task<ServiceResult<TagView>> Handle(CreateTagRequest request, CancellationToken cancelToken)
    {
        try
        {
            UpsertTagDto dto = request.Tag;

            // Shape validation first, so a malformed request never touches the database.
            List<string> errors = TagValidation.Validate(dto);

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(CreateTagRequest), string.Join("; ", errors));
                return ServiceResult<TagView>.Fail(errors);
            }

            // Code is the unique natural key — reject a duplicate before inserting.
            bool codeExists = await context.Tags.AnyAsync(t => t.Code == dto.Code, cancelToken);

            if (codeExists)
            {
                return ServiceResult<TagView>.Conflict($"A tag with code '{dto.Code}' already exists.");
            }

            var tag = new Tag
            {
                Code = dto.Code,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                TagType = dto.TagType,
                MovementDomain = dto.MovementDomain,
                LeadershipModel = dto.LeadershipModel,
                UnledBaseSize = dto.UnledBaseSize,
                UnledDefaultSize = dto.UnledDefaultSize,
                UnledMaxSize = dto.UnledMaxSize,
                SystemCreated = dto.SystemCreated,
            };

            context.Tags.Add(tag);
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Created tag '{Code}' (id {Id}).", tag.Code, tag.Id);

            return ServiceResult<TagView>.Created(ToView(tag));
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(CreateTagHandler) + ".";
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
            MovementDomain = tag.MovementDomain,
            LeadershipModel = tag.LeadershipModel,
            UnledBaseSize = tag.UnledBaseSize,
            UnledDefaultSize = tag.UnledDefaultSize,
            UnledMaxSize = tag.UnledMaxSize,
            SystemCreated = tag.SystemCreated,
        };
    }
}
