namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The identity of a successfully authenticated user. Returned by the register and login
/// handlers; the API layer turns this into a signed user token.
/// </summary>
public record AuthenticatedUser(int UserId, string Username);
