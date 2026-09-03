using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Enums.Authentication;

using MediatR;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Verifies a username and password and, on success, returns the user's identity.
/// </summary>
public record LoginRequest(
    string Username,
    string Password
) : IRequest<ServiceResult<AuthenticatedUser>>;

internal class LoginRequestValidator : Validator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

internal class LoginHandler(
    ILogger<LoginHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IPasswordHasher<User> passwordHasher
) : IRequestHandler<LoginRequest, ServiceResult<AuthenticatedUser>>
{
    // A single message for both "no such user" and "wrong password" so we don't reveal
    // which usernames exist.
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    public async Task<ServiceResult<AuthenticatedUser>> Handle(LoginRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            string username = request.Username.Trim();
            User? user = await context.Users.FirstOrDefaultAsync(u => u.Username == username, cancelToken);

            if (user is null)
            {
                logger.LogInformation("Login failed: no user named {Username}.", username);
                return ServiceResult<AuthenticatedUser>.Forbidden(InvalidCredentialsMessage);
            }

            PasswordVerificationResult verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verification == PasswordVerificationResult.Failed)
            {
                logger.LogInformation("Login failed: wrong password for user {Username}.", username);
                return ServiceResult<AuthenticatedUser>.Forbidden(InvalidCredentialsMessage);
            }

            // If the hasher says the stored hash uses outdated parameters, transparently upgrade it.
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                await context.SaveChangesAsync(cancelToken);
            }

            logger.LogInformation("User {Username} (Id {UserId}) logged in.", user.Username, user.Id);

            // Read the grants now so the token the API mints carries them; a change made after this point
            // takes effect on the user's next refresh.
            IReadOnlyCollection<PermissionType> permissions = await context.GetUserPermissionsAsync(user.Id, cancelToken);

            return ServiceResult<AuthenticatedUser>.Succeed(new AuthenticatedUser(user.Id, user.Username, permissions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(LoginRequest));
            return ServiceResult<AuthenticatedUser>.InternalServerError("An unexpected error occurred while logging in.");
        }
    }
}
