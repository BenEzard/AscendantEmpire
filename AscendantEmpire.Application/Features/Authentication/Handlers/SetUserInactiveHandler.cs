using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

public record SetUserInactiveRequest(
    int UserId,
    InactiveReasonType InactiveReasonType
) : IRequest<ServiceResultBase>;

internal class SetUserInactiveRequestValidator : Validator<SetUserInactiveRequest>
{
    public SetUserInactiveRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User Id is required.");

        RuleFor(x => x.InactiveReasonType)
            .IsInEnum()
            .WithMessage("Inactive reason type must be a valid enum value.");
    }
}

internal class SetUserInactiveHandler(
    ILogger<SetUserInactiveHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<SetUserInactiveRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SetUserInactiveRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new SetUserInactiveRequestValidator();
            var validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                const string error = "Validation failed for SetUserInactiveRequest.";

                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResultBase.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancelToken);
            if (user is null)
            {
                return ServiceResultBase.NotFound($"User {request.UserId} was not found.");
            }

            user.InactiveReasonType = request.InactiveReasonType;
            user.InactiveAt = request.InactiveReasonType == InactiveReasonType.Active
                ? null
                : DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancelToken);

            return ServiceResultBase.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "Unhandled exception setting auth user inactivity.";

            logger.LogError(ex, error);

            return ServiceResultBase.Fail([error, ex.Message]);
        }
    }
}