namespace AscendantEmpire.Application.Features.Authentication.Classes;

/// <summary>
/// The result of creating a new login session. 
/// </summary>
/// <remarks>
/// <see cref="RefreshToken"/> is the raw, one-time secret to hand back to the client — it is never stored server-side (only its hash is).
/// <see cref="SessionId"/> is baked into the access token so later calls can act on the session.</remarks>
public record IssuedSession(int SessionId, string RefreshToken);
