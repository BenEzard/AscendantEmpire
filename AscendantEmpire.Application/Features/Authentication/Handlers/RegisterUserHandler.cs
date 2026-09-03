using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using MediatR;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Creates a new user account from a chosen username and password.
/// </summary>
public record RegisterUserRequest(
    string Username,
    string Password,
    string? Email
) : IRequest<ServiceResult<AuthenticatedUser>>;

internal class RegisterUserRequestValidator : Validator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(User.USERNAME_MAX_LENGTH).WithMessage($"Username must be {User.USERNAME_MAX_LENGTH} characters or fewer.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(User.EMAIL_MAX_LENGTH).WithMessage($"Email must be {User.EMAIL_MAX_LENGTH} characters or fewer.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => string.IsNullOrWhiteSpace(x.Email) == false);
    }
}

internal class RegisterUserHandler(
    ILogger<RegisterUserHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IPasswordHasher<User> passwordHasher
) : IRequestHandler<RegisterUserRequest, ServiceResult<AuthenticatedUser>>
{
    /// <summary>
    /// What a brand-new account is granted: the two ordinary player capabilities, and nothing that reaches a developer tool.
    /// </summary>
    private static readonly IReadOnlyCollection<PermissionType> DefaultPermissions = [PermissionType.CreateGame, PermissionType.JoinGame];

    public async Task<ServiceResult<AuthenticatedUser>> Handle(RegisterUserRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Usernames are the login key, so reject duplicates up front with a clear message.
            // (The unique index is the real guarantee; this just gives a friendlier error.)
            string username = request.Username.Trim();
            bool usernameTaken = await context.Users.AnyAsync(u => u.Username == username, cancelToken);

            if (usernameTaken)
            {
                logger.LogInformation("Registration rejected: username {Username} is already taken.", username);
                return ServiceResult<AuthenticatedUser>.Conflict("That username is already taken.");
            }

            User user = new()
            {
                Username = username,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                // Never store the plaintext password — only the hash.
                PasswordHash = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                InactiveReasonType = InactiveReasonType.Active,
            };

            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            context.Users.Add(user);
            await context.SaveChangesAsync(cancelToken);

            // Grant the two ordinary player capabilities so a new account can play straight away. The three
            // developer-tool permissions are never automatic — someone with the System panel grants those.
            DateTimeOffset grantedAt = DateTimeOffset.UtcNow;

            foreach (PermissionType permission in DefaultPermissions)
            {
                context.UserPermissions.Add(new UserPermission
                {
                    UserId = user.Id,
                    Permission = permission,
                    GrantedAt = grantedAt,
                });
            }

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Registered new user {Username} (Id {UserId}).", user.Username, user.Id);

            return ServiceResult<AuthenticatedUser>.Created(new AuthenticatedUser(user.Id, user.Username, DefaultPermissions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(RegisterUserRequest));
            return ServiceResult<AuthenticatedUser>.InternalServerError("An unexpected error occurred while registering the account.");
        }
    }
}
