using AscendantEmpire.Domain.Enums.Authentication;

namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The identity of a successfully authenticated user. Returned by the register and login
/// handlers; the API layer turns this into a signed user token.
/// </summary>
/// <remarks>
/// <see cref="Permissions"/> is positional rather than optional on purpose: every caller must decide what to put in it, so no path can quietly mint a
/// token with no permissions in it.
/// </remarks>
public record AuthenticatedUser(int UserId, string Username, IReadOnlyCollection<PermissionType> Permissions);
