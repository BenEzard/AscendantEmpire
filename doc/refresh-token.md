# Revocable Logins via Refresh Tokens

## Context

Auth today (G-11) is **pure stateless JWT**: once a 120-minute access token is issued there is
no way to invalidate it before expiry. So if a server admin deletes a game (or kicks a player),
anyone already holding a game token keeps working for up to 2 hours — game-scoped endpoints read
`gameId`/`playerId` straight from the token, not the DB.

To make logins revocable we introduce a **server-side session** backed by an opaque, rotating
**refresh token**, and shorten access-token lifetime. Decisions confirmed with the user:

- **Refresh-token model** — login issues a short-lived access token + a stored refresh token.
- **One session per refresh token**, with the game context (`gameId`/`playerId`) stored *on the
  session*. `enter-game` updates that context; `refresh` re-issues the correctly-scoped access
  token. Revoking "by game" = revoking every session whose `GameId` matches.
- **Short access lifetime (~5 min)** for enforcement — revoking a session logs the player out on
  their next refresh, i.e. within the access-token window. No per-request DB check, no realtime
  socket. (Trade-off: a kick is effective within ~5 min, not literally the next call.)

This is the auth infrastructure that the broader permissions/server-admin feature (delete-game,
admin role) will hook into; the admin delete-game endpoint itself is **out of scope here** — this
work delivers the revocation mechanism and the service method it will call.

## Design Overview

Add a `RefreshSession` row per login. Access tokens gain a `sessionId` claim linking back to it.
A short-lived access token is refreshed via a new `/auth/refresh` endpoint that validates the
session, rotates the refresh token, and re-issues a user- or game-scoped access token based on the
session's stored game context. Deleting a game / kicking a player / banning an account = mark the
matching sessions revoked; the next refresh then fails and the client is logged out.

## Backend Changes

### 1. New entity — `RefreshSession` (auth schema)

`AscendantEmpire.Domain\Entities\Identity\RefreshSession.cs`:

- `Id` (PK), `UserId` (FK → User)
- `TokenHash` (string) — SHA-256 hash of the opaque refresh token; **never store raw**
- `CreatedAt`, `ExpiresAt` (DateTimeOffset) — absolute lifetime from `RefreshTokenDays`
- `RevokedAt` (DateTimeOffset?) — null = active
- `GameId` (int?), `PlayerId` (int?) — current game context (null until `enter-game`)

Register in `AscendantEmpireContext.cs` → `ConfigureAuthSchema` (alongside User/Player, ~line 561):
table `RefreshSessions` in `auth` schema; **unique** index on `TokenHash`; non-unique indexes on
`GameId` and `UserId` (revocation lookups); FK to User with `OnDelete(Cascade)`. Add
`DbSet<RefreshSession>` and create an EF migration.

### 2. Token service — `ITokenService` / `JwtTokenService.cs`

- Add a `sessionId` parameter to `CreateUserToken` / `CreateGameToken`; emit a new
  `AuthClaimTypes.SessionId` claim (add the constant; add `GetSessionId()` to
  `ClaimsPrincipalExtensions.cs`).
- Add `GenerateRefreshToken()` → cryptographically random URL-safe string, and
  `HashRefreshToken(string raw)` → SHA-256 hex (used to store and to look up).

### 3. Config — `JwtSettings.cs` + appsettings

- `AccessTokenMinutes`: 120 → **5** (set in `appsettings.json` / `appsettings.Development.json`).
- Add `RefreshTokenDays` (default 14).

### 4. Contract — `IAuthTokenResult` / `DTAuthTokenResult`

- Add `string? RefreshToken`. Populated on register/login/refresh; left null on `enter-game`
  (same session — the client keeps its existing refresh token).

### 5. Endpoints — `AuthController.cs` (+ MediatR handlers under `Features\Authentication`)

- **register / login**: create a `RefreshSession` (GameId null), return access token (with
  `sessionId` claim) + raw refresh token. New handler/service to create the session row.
- **enter-game**: read `userId` + `sessionId` from the token; resolve the player via the existing
  `EnterGameHandler` lookup; **update the session row** with `GameId`/`PlayerId`; issue a new
  game-scoped access token carrying the same `sessionId`.
- **`POST /auth/refresh`** (new, `[AllowAnonymous]`, body `{ refreshToken }`): hash → find session
  by `TokenHash` → reject if missing/revoked/expired (401). Re-issue access token scoped by
  `session.GameId` (game token if set, else user token). **Rotate**: generate a new refresh token,
  update `TokenHash` + `CreatedAt` in place (keep `ExpiresAt` fixed). Return new access + refresh.
- **`POST /auth/logout`** (new, `[Authorize]`): read `sessionId` from token, set `RevokedAt`.

### 6. Revocation service (Application layer)

A `SessionRevoker` service / MediatR commands the rest of the app calls:

- `RevokeByGame(gameId)` — revoke active sessions where `GameId == gameId` (**the delete-game /
  kick-all hook**).
- `RevokeByPlayer(playerId)` — kick a single player.
- `RevokeByUser(userId)` — account ban / forced password reset.

Wiring `RevokeByGame` into an actual admin delete-game endpoint is deferred to the
permissions/server-admin feature.

## Frontend Changes

### `src/api/authToken.ts`

- Add `refreshToken` to `AuthState`; persist it; add `getRefreshToken()`. Keep using existing
  `setAuth`/`clearAuth`.
- **Security note:** localStorage refresh tokens are XSS-exposed; an httpOnly cookie is more
  secure but needs CORS-credentials + CSRF handling — a bigger change. This keeps localStorage for
  consistency with the current access-token storage; flagging the trade-off.

### `src/api/http.ts`

Replace the current "401 → clearAuth + reload" with the standard refresh-interceptor pattern:
on a 401, call `/auth/refresh` with the stored refresh token; on success, persist the new
access/refresh tokens and **retry the original request**; on refresh failure, `clearAuth()` +
reload. Guard with a **single-flight** refresh promise (queue concurrent 401s onto one refresh)
and a per-request retry flag to avoid loops.

### Login / enter-game callers

Persist `refreshToken` from the auth response into `AuthState` (login/register set it; enter-game
leaves the existing one untouched).

## Verification

- **Backend handler/service tests**: refresh with a valid token rotates and returns a working
  access token; expired/revoked/unknown refresh tokens return 401; `RevokeByGame` marks exactly
  the matching sessions; refresh after `enter-game` yields a game-scoped token.
- **Frontend (Vitest, from G-13)**: interceptor refreshes on 401 and retries once; concurrent 401s
  trigger a single refresh; failed refresh clears auth.
- **End-to-end manual**: log in → confirm a `RefreshSession` row + 5-min access token; enter a
  game → session row gains `GameId`/`PlayerId`; wait past 5 min and make a call → interceptor
  refreshes transparently; call `RevokeByGame` (or revoke the row directly) → the next refresh
  401s and the client logs out.
- `dotnet build` + `dotnet ef migrations` apply cleanly; run the API and exercise
  register/login/enter-game/refresh/logout.
