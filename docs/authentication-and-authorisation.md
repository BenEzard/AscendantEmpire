# Authentication & Authorisation

This document describes how users log in, how the API knows who they are, and how a request
ends up scoped to a specific game and player. It covers the token model, the backend pieces,
the frontend wiring, configuration, and how to extend the system.

---

## Concepts

There are three things to keep distinct:

| Concept | What it is |
|---|---|
| **User** | An account a person logs in with (username + password). Lives in `auth.Users`. |
| **Player** | A participant *within a single game* (a ruler/faction). Lives in `game.Player`. |
| **Game** | A single match. Lives in `game.GameIndex`. |

The relationship rules:

- A **User** owns **one or more Players** — at most **one Player per Game**, but possibly
  **many Games at once**.
- This "one player per user per game" rule is enforced in the database by a filtered unique
  index `UX_Player_Game_User` on `game.Player(GameId, UserId)`.

Because a user can be in several games at once, **logging in cannot pick a game for you**.
Authentication happens in two steps:

1. **Log in** — proves *who you are* (issues a user token).
2. **Enter a game** — the server looks up *which player you are in that game* and issues a new
   token that also carries the game and player.

---

## Token model

Auth is **stateless** and based on **JWT bearer tokens**. There is no server-side session;
everything the API needs is signed into the token. Two token shapes are produced from the same
signing key:

| Token | Issued by | Claims it carries |
|---|---|---|
| **User token** | `POST /auth/register`, `POST /auth/login` | `userId`, `username` |
| **Game token** | `POST /auth/enter-game` | `userId`, `username`, `gameId`, `playerId` |

The client stores whichever token is current and sends it on every request as:

```
Authorization: Bearer <token>
```

Entering a game **replaces** the user token with a game token. Switching games means calling
`enter-game` again to get a fresh game token.

```
                 ┌─────────────┐   user token    ┌──────────────┐  game token   ┌───────────┐
  register/login │  /auth/...  │ ───────────────▶ │ /auth/enter- │ ────────────▶ │  protected│
  (username+pwd) │             │   (who you are)  │    game      │ (+game+player)│  endpoints │
                 └─────────────┘                  └──────────────┘               └───────────┘
```

### Claim names

Defined once in [`AuthClaimTypes`](../AscendantEmpire.Api/Authentication/AuthClaimTypes.cs) and
shared between the code that writes tokens and the code that reads them:

| Claim | Present in | Meaning |
|---|---|---|
| `userId` | every token | the account id |
| `username` | every token | the login name |
| `gameId` | game token only | the entered game |
| `playerId` | game token only | the player controlled in that game |

---

## API endpoints

All live on [`AuthController`](../AscendantEmpire.Api/Controllers/AuthController.cs).

### `POST /auth/register`  *(anonymous)*
Creates an account and returns a **user token**.

```jsonc
// request
{ "username": "alice", "password": "at-least-8-chars", "email": "optional@example.com" }
// response (201)
{ "token": "eyJ...", "username": "alice", "gameId": null, "playerId": null }
```
- `409 Conflict` if the username is taken.
- `400` if validation fails (username required, password ≥ 8 chars, email valid if supplied).

### `POST /auth/login`  *(anonymous)*
Verifies credentials and returns a **user token**. Same response shape as register.
- `403 Forbidden` with a generic *"Invalid username or password."* for both a wrong password
  and an unknown user (so the API doesn't reveal which usernames exist).

### `POST /auth/enter-game`  *(requires a valid user token)*
Resolves the caller's player in the given game and returns a **game token**.

```jsonc
// request
{ "gameId": 7 }
// response (200)
{ "token": "eyJ...", "username": "alice", "gameId": 7, "playerId": 70 }
```
- The `userId` comes from the **token**, never the request body.
- `403 Forbidden` if the user controls no player in that game.
- `401 Unauthorized` if the token is missing/expired/invalid.

---

## How endpoints read the game & player

Protected endpoints no longer take `gameId`/`playerId` from the query string or body — they
read them from the token's claims. This means a caller **cannot ask for another player's data**.

[`PlayerController`](../AscendantEmpire.Api/Controllers/PlayerController.cs) is the reference
example. It is decorated with `[Authorize]` and pulls the context out of the token via
[`ClaimsPrincipalExtensions`](../AscendantEmpire.Api/Utilities/ClaimsPrincipalExtensions.cs):

```csharp
// Game and player come from the token, so the caller can't ask for someone else's data.
if (!TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
{
    return contextError!; // 403 when the token has no game context (user token only)
}

var response = await mediator.Send(new GetPlayerStructuresRequest(gameId, playerId), cancelToken);
```

The request/handler signatures are unchanged — only the *source* of the ids changed.
This is the pattern to apply to other controllers.

### Status codes

| Situation | Result |
|---|---|
| No token / expired / tampered | `401` (handled by the JWT middleware before the action runs) |
| Valid user token but no game entered, on a game endpoint | `403` ("No game entered. Call /auth/enter-game first.") |
| Authenticated but not allowed to touch a resource | `403` via `ServiceResult.Forbidden(...)` |

---

## Backend components

| Piece | Location | Role |
|---|---|---|
| `User` entity | `AscendantEmpire.Domain/Entities/Identity/User.cs` | The account record. |
| Schema config | `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` (`ConfigureAuthSchema`) | `auth.Users`, unique username, `Player→User` FK, `UX_Player_Game_User`. |
| Register/Login/EnterGame handlers | `AscendantEmpire.Application/Features/Authentication/Handlers/` | Business logic; return identity data, **not** tokens. |
| `PasswordHasher<User>` | registered in `ApplicationStartup.cs` | Hashes & verifies passwords (from `Microsoft.Extensions.Identity.Core`; the full ASP.NET Identity stack is **not** used). |
| `ITokenService` / `JwtTokenService` | `AscendantEmpire.Api/Authentication/` | Builds the signed JWTs. API-layer concern. |
| `AuthController` | `AscendantEmpire.Api/Controllers/AuthController.cs` | The endpoints; turns handler results into tokens. |
| `ClaimsPrincipalExtensions` | `AscendantEmpire.Api/Utilities/` | `GetUserId()`, `GetGameId()`, `GetPlayerId()`. |
| Auth wiring | `AscendantEmpire.Api/Program.cs` | `AddAuthentication().AddJwtBearer(...)`, `UseAuthentication()`, Swagger bearer button. |

> **Design note:** handlers return identity DTOs (`AuthenticatedUser`, `GameContext`) and the
> *controller* builds the token. This keeps JWT concerns out of the Application layer, so the
> handlers stay easy to unit-test.

### Database

A migration `AddUsersAndAuth` creates the `auth` schema and table. Apply it with:

```
dotnet ef database update --project AscendantEmpire.Application --startup-project AscendantEmpire.Api
```

`auth.Users`:

| Column | Type | Notes |
|---|---|---|
| `Id` | int, identity | PK |
| `Username` | nvarchar(50) | required, **unique** |
| `Email` | nvarchar(256) | optional |
| `PasswordHash` | nvarchar(256) | required; hash only, never plaintext |
| `CreatedAt` | datetimeoffset | required |

`game.Player.UserId` is a nullable FK to `auth.Users.Id` (null = an unassigned/AI slot). The
`UX_Player_Game_User` index is filtered to `WHERE [UserId] IS NOT NULL`, so many unassigned
slots can coexist while still guaranteeing one assigned player per user per game.

---

## Configuration

The `Jwt` settings section (bound to
[`JwtSettings`](../AscendantEmpire.Api/Authentication/JwtSettings.cs)):

```jsonc
"Jwt": {
  "Issuer": "AscendantEmpire.Api",
  "Audience": "AscendantEmpire.Client",
  "AccessTokenMinutes": 120
  // SigningKey is NOT here — see below
}
```

### The signing key (important)

The signing key is the secret that makes tokens unforgeable. **Do not commit a real one.**

- A clearly-labelled **dev-only** key lives in `appsettings.Development.json` so the app runs
  locally out of the box.
- For anything beyond local dev, set a real secret out of source control:

```
dotnet user-secrets set "Jwt:SigningKey" "<a long random string, 32+ chars>" --project AscendantEmpire.Api
```

Rotating the key invalidates every existing token (everyone is logged out). The app **fails to
start** if no signing key is configured, rather than issuing unverifiable tokens.

---

## Frontend

The React app (`AscendantEmpire.AscendantEmpireUI`) stores **one token at a time** in
`localStorage`.

| Piece | Location | Role |
|---|---|---|
| Token storage | `src/api/authToken.ts` | `getAuth/setAuth/clearAuth/getToken`. |
| HTTP wiring | `src/api/http.ts` | Request interceptor attaches `Authorization: Bearer <token>`; response interceptor on **401** clears the token and reloads to the login screen. |
| Auth calls | `src/features/auth/api/authApi.ts` | `register`, `login`, `enterGame` — each stores the returned token. |
| Login UI | `src/features/auth/components/LoginForm.tsx` | Login / register form. |
| Gate | `src/features/auth/components/AuthGate.tsx` | No token → login form; otherwise the app + a logout control. |
| Game entry | `src/entries/game.tsx` | Wraps `GameUI` in `AuthGate`. |
| Enter-game wiring | `src/features/game/components/GameUI.tsx` | Selecting a game calls `enterGame(gameId)` to swap the user token for a game token before the game loads. |

Flow: load the game page → (no token) login form → log in → open a game (calls `enter-game`)
→ game UI loads with a game token. A reload keeps you logged in (token persists in
`localStorage`). Logout clears the token and reloads.

> **localStorage trade-off:** simple, but readable by any script running on the page, so it's
> exposed if the site ever has an XSS bug. Acceptable for development; an `httpOnly` cookie is
> the more hardened alternative (at the cost of CSRF handling and extra CORS/cookie setup).

---

## Known gap: linking users to players

This first pass builds the auth system and converts one representative slice. It does **not**
include a way to attach a freshly-registered user to a player slot, so:

> After you register, `enter-game` will return **403** for every game until your account is
> linked to a player (`Player.UserId` is `null` for all existing rows).

To link manually while testing:

```sql
UPDATE game.Player
SET UserId = (SELECT Id FROM auth.Users WHERE Username = 'yourname')
WHERE Id = <the player id you want to control>;
```

A proper "claim a player slot" / "assign player to user" feature is the natural next step.

---

## Security baseline & limits

What this gives you:

- Passwords stored only as `PasswordHasher` hashes, never plaintext.
- Signed, expiring tokens; tampered or foreign tokens are rejected (issuer, audience, signature
  and lifetime are all validated, with **no** clock-skew grace).
- Game/player read from the token, so a caller can't request another player's data on converted
  endpoints.
- Generic login errors that don't reveal which usernames exist.

Out of scope for this pass (reasonable follow-ups):

- **Linking users to players** (see the gap above).
- **Converting the remaining controllers** to read ids from claims (only `PlayerController` is
  done; everything else still takes ids in the body and is currently unauthenticated).
- **Scoping list endpoints** like `/games/open` to the logged-in user.
- **Refresh tokens / "remember me"** — currently you re-login when the token expires.
- **Roles / permissions** beyond ownership (e.g. an admin role).

---

## Extending: protecting another controller

To apply the pattern to a new controller:

1. Add `[Authorize]` to the controller (or specific actions).
2. Remove `gameId`/`playerId` from the action parameters.
3. Read them from the token via `User.GetGameId()` / `User.GetPlayerId()` (guard for the
   user-token-only case and return `403`, as `PlayerController` does).
4. Build the existing request with those values.

No handler or Application-layer changes are needed — the request/handler contracts stay the same.
