# Match Maker

The Match Maker lets a person create an account, log in, and **claim a player slot** in an open
game — closing the "linking users to players" gap called out in
[authentication-and-authorisation.md](authentication-and-authorisation.md). Once a slot is
claimed the user can jump straight into the game.

This document covers the concept, the backend endpoints, the frontend subsystem, and how it fits
with the existing auth flow.

---

## Concept

A **player slot** is just a `game.Player` row. When a game is set up,
[`GameSetupService`](../AscendantEmpire.Application/Features/Game/Services/GameSetupService.cs)
creates one `Player` per seat with `UserId = null` (unassigned) and `Status = Active`.

- **Claiming** a slot sets that `Player.UserId` to the logged-in account.
- A user may hold **at most one slot per game**, enforced by the filtered unique index
  `UX_Player_Game_User` on `game.Player(GameId, UserId)`. The explicit checks in the handler turn
  a lost race into a friendly error; the index is the real guarantee.

Claiming does **not** change the auth model — it just fills in the `UserId` that
`POST /auth/enter-game` later looks for. Before this feature you had to assign players with raw
SQL; now the Match Maker does it.

```
 register / log in        claim a slot                 enter the game
 (user token)             (Player.UserId = you)        (game token)         play
   AuthGate     ─────▶    POST /matchmaking/claim ─────▶ POST /auth/enter-game ─────▶ /game/
```

---

## API endpoints

All live on
[`MatchmakingController`](../AscendantEmpire.Api/Controllers/MatchmakingController.cs). The whole
controller is `[Authorize]`, so a **user token is required**. The account is always read from the
token (`User.GetUserId()`), never from the request body.

### `GET /matchmaking/games`

Lists the joinable games and every **active** slot in each, flagged as claimed or free.

A game is joinable when it has finished setup and is in progress
(`CreationStatus == SetupComplete && PlayStatus == InProgress`). Both claimed and unclaimed slots
are returned so the UI can show taken vs free seats.

```jsonc
// response (200)
{
  "games": [
    {
      "gameId": 7,
      "gameName": "First Blood",
      "numberOfPlayers": 4,
      "mapWidth": 64,
      "mapHeight": 64,
      "slots": [
        { "playerId": 70, "rulerTitle": "King", "rulerName": "Player 1", "factionName": "Faction 1",
          "rulerLabel": "King Player 1 (Faction 1)", "isClaimed": true,  "isClaimedByCurrentUser": true },
        { "playerId": 71, "rulerTitle": "Queen", "rulerName": "Player 2", "factionName": "Faction 2",
          "rulerLabel": "Queen Player 2 (Faction 2)", "isClaimed": true,  "isClaimedByCurrentUser": false },
        { "playerId": 72, "rulerTitle": "Duke", "rulerName": "Player 3", "factionName": "Faction 3",
          "rulerLabel": "Duke Player 3 (Faction 3)", "isClaimed": false, "isClaimedByCurrentUser": false }
      ]
    }
  ]
}
```

- `isClaimed` — some account holds the slot; it can't be claimed. The claiming user's identity is
  **not** revealed.
- `isClaimedByCurrentUser` — the requesting account holds this slot (UI shows it as "your slot").

### `POST /matchmaking/claim`

Claims one unassigned slot for the logged-in user.

```jsonc
// request
{ "gameId": 7, "playerId": 72 }
// response: 204 No Content
```

| Situation | Result |
|---|---|
| Claimed | `204 No Content` |
| Validation failed (ids must be > 0) | `400` |
| Missing / invalid token | `401` |
| Slot doesn't exist in that game | `404` |
| Slot already taken, **or** caller already controls a player in that game | `409` |
| Slot exists but isn't `Active` (eliminated/banned) | `409` |

---

## Backend components

| Piece | Location | Role |
|---|---|---|
| `MatchmakingController` | `AscendantEmpire.Api/Controllers/MatchmakingController.cs` | `[Authorize]` endpoints; reads the account from the token. |
| `GetMatchmakingGamesHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Lists joinable games + slots, deriving the claim flags per slot. |
| `ClaimPlayerSlotHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Assigns `Player.UserId`; guards "already taken" / "already in this game". |
| Backend contracts | `AscendantEmpire.BackendContracts/MatchMaking/` | `MatchmakingGame`, `MatchmakingSlot` (handler output), `ClaimPlayerSlotDto` (request body). |
| Frontend contracts | `AscendantEmpire.FrontendContracts/MatchMaking/` | `IMatchmakingGameCollection`, `IMatchmakingGame`, `IMatchmakingSlot`. |
| DTOs | `AscendantEmpire.Application/Features/MatchMaking/Classes/` | `DTMatchmakingGameCollection` etc. — the serialized shapes. |

Handlers are auto-registered by MediatR's assembly scan, and their FluentValidation validators by
the existing validation pipeline — no DI wiring was added.

> **Design note:** this mirrors the auth handlers — the handler returns a `ServiceResult` and the
> controller maps it to an HTTP result. The claim handler returns `ServiceResult<Empty>`; the
> controller treats it as a bare status (`ToActionResult()`), so a successful claim is a clean
> `204` with no body.

---

## Frontend (the `matchmaker` subsystem)

The UI is a separate page entry, exactly like `game` and `mapeditor`. Each subsystem is its own
HTML document so a tab only ever mounts one feature.

| Piece | Location | Role |
|---|---|---|
| HTML entry | `AscendantEmpire.AscendantEmpireUI/matchmaker/index.html` | The page served at `/matchmaker/`. |
| Vite input | `vite.config.ts` (`rollupOptions.input.matchmaker`) | Registers the page for the build. |
| React entry | `src/entries/matchmaker.tsx` | Mounts `MatchMaker` inside `AuthGate` + `AppProviders`. |
| Main component | `src/features/matchmaker/components/MatchMaker.tsx` | Lists games/slots, claims a free slot, "Play now" on a held slot. |
| API calls | `src/features/matchmaker/api/matchmakingApi.ts` | `getMatchmakingGames()`, `claimPlayerSlot(gameId, playerId)`. |
| Types | `src/features/matchmaker/types/` | Camel-cased mirrors of the frontend contracts. |
| Landing link | `index.html` | Adds a "Match Maker" link next to Game / Map Editor. |

### Flow

1. Open `/matchmaker/`. With no token, `AuthGate` shows the existing login / register form.
2. Once logged in, `MatchMaker` lists joinable games. Each slot is one of:
   - **Your slot** — badge + a **Play now** button.
   - **Taken** — a muted badge (no name shown).
   - **Free** — a **Claim** button (disabled if you already hold a slot in that game).
3. **Claim** calls `POST /matchmaking/claim`, then refetches the list so the seat flips to "your
   slot".
4. **Play now** calls `enterGame(gameId)` (from the shared auth API) to swap the user token for a
   game token, then redirects to `/game/`.

Auth reuse is deliberate: registration, login, the token store (`src/api/authToken.ts`), and the
`Authorization` header (`src/api/http.ts`) are all the existing auth plumbing — the Match Maker
just adds the claim step on top.

---

## Limits & follow-ups

- **No release / leave** — you can claim a slot but not give it back. Releasing would need a
  guarded endpoint (only the holder; consider what happens to in-progress game state).
- **Claimants are hidden** — taken slots don't show who holds them. Surfacing usernames (or
  display names) would be a small contract addition if a lobby view wants it.
- **`/games/open` is unchanged** — it stays anonymous and only lists `Active` players. The Match
  Maker uses its own authorised listing so it can compute the per-user claim flags.
- **Slot identity is cosmetic** — ruler title/name/faction are auto-generated at setup; letting a
  user rename their ruler on claim is a natural extension.
