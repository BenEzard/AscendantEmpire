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
   AuthGate     ─────▶    POST /match-making/claim ─────▶ POST /auth/enter-game ─────▶ /game/
```

---

## API endpoints

All live on
[`MatchMakingController`](../AscendantEmpire.Api/Controllers/MatchMakingController.cs). The whole
controller is `[Authorize]`, so a **user token is required**. The account is always read from the
token (`User.GetUserId()`), never from the request body.

### `GET /match-making/games`

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
          "factionColour": "#0069a8",
          "rulerLabel": "King Player 1 (Faction 1)", "isClaimed": true,  "isClaimedByCurrentUser": true },
        { "playerId": 71, "rulerTitle": "Queen", "rulerName": "Player 2", "factionName": "Faction 2",
          "factionColour": "#c10007",
          "rulerLabel": "Queen Player 2 (Faction 2)", "isClaimed": true,  "isClaimedByCurrentUser": false },
        { "playerId": 72, "rulerTitle": "Nobody", "rulerName": "Player 3", "factionName": "Faction 3",
          "factionColour": null,
          "rulerLabel": "Nobody Player 3 (Faction 3)", "isClaimed": false, "isClaimedByCurrentUser": false }
      ]
    }
  ]
}
```

- `isClaimed` — some account holds the slot; it can't be claimed. The claiming user's identity is
  **not** revealed.
- `isClaimedByCurrentUser` — the requesting account holds this slot (UI shows it as "your slot").
- `factionColour` — the colour the slot's player picked, or `null` while nobody has. `rulerGender`
  is deliberately **not** here: nothing renders it, and it is the one identity value that isn't
  obviously public.
- Slots are ordered by `playerId`, not by label — players rename themselves once they claim a seat,
  and ordering by the label would make the rows jump about between refreshes while a lobby fills.

### `POST /match-making/claim`

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

### `GET /match-making/games/{gameId}/my-player`

Reads back the caller's own ruler and faction identity in one game, plus the colours their rivals
already hold. This is what the setup dialog opens against.

The route names the **game**, never the player: the slot within it is resolved from the token's user
id, so the request cannot describe anyone else's seat. A caller who controls no slot in that game
gets a `404` rather than a `403` — there is nothing there to forbid them from.

```jsonc
// response (200)
{
  "playerId": 72,
  "gameId": 7,
  "rulerTitle": "Nobody",
  "rulerName": "Player 3",
  "rulerGender": null,          // "Male" | "Female" | null
  "factionName": "Faction 3",
  "factionColour": null,
  "usedFactionColours": [
    { "value": "#0069a8", "usedBy": "Faction 1" },
    { "value": "#c10007", "usedBy": "Faction 2" }
  ]
}
```

`usedFactionColours` is taken here rather than derived from the games listing the client already
holds: that listing is cached, and a stale reading would offer a colour that has since been taken.
It never includes the caller's own colour, which must stay pickable.

### `PUT /match-making/games/{gameId}/my-player`

Defines the caller's own ruler and faction. A full replacement of the five values, not a patch.

Gated on `JoinGame`, the same permission as claiming. The body carries **no** identity at all — the
game comes from the route and the slot from the token — so it cannot name a slot the caller does not
own.

```jsonc
// request
{ "rulerTitle": "Empress", "rulerName": "Aelara", "rulerGender": "Female",
  "factionName": "The Ember Pact", "factionColour": "#c800de" }
// response: 204 No Content
```

| Situation | Result |
|---|---|
| Saved | `204 No Content` |
| Validation failed (see below) | `400` |
| Missing / invalid token | `401` |
| Token lacks `JoinGame` | `403` |
| Caller controls no player in that game | `404` |
| Another player in that game already holds that colour | `409` |

- **`rulerTitle` is optional.** Blank (or omitted) is a valid choice and is stored as `"Nobody"`,
  the same placeholder `GameSetupService` seeds a slot with. The substitution happens in the
  handler, not as a database default, so there is one place that decides it.
- **`rulerGender` is required** and must be `"Male"` or `"Female"`. The enum starts at 1, so an
  omitted value fails validation rather than quietly arriving as `Male`.
- **`factionColour` is required** and must match `^#[0-9a-fA-F]{6}$`. It is stored lower-cased, and
  is unique within the game — enforced by the filtered unique index `UX_Player_Game_FactionColour`
  on `game.Player(GameId, FactionColour)`, with an up-front check in the handler that turns the
  common case into a friendly `409`.
- The colour is **not** checked against the frontend's palette. Doing so would mean keeping a copy
  of that palette in C# forever, and the worst a hostile client achieves by sending something else
  is an ugly colour.

---

## Backend components

| Piece | Location | Role |
|---|---|---|
| `MatchMakingController` | `AscendantEmpire.Api/Controllers/MatchMakingController.cs` | `[Authorize]` endpoints; reads the account from the token. |
| `GetMatchMakingGamesHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Lists joinable games + slots, deriving the claim flags per slot. |
| `ClaimPlayerSlotHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Assigns `Player.UserId`; guards "already taken" / "already in this game". |
| `GetPlayerDefinitionHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Reads the caller's own slot plus the rivals' colours, in one query. |
| `UpdatePlayerDefinitionHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Writes the five identity values; defaults a blank title and guards the colour clash. |
| Backend contracts | `AscendantEmpire.BackendContracts/MatchMaking/` | `MatchMakingGame`, `MatchMakingSlot`, `PlayerDefinition`, `UsedFactionColour` (handler output); `ClaimPlayerSlotDto`, `UpdatePlayerDefinitionDto` (request bodies). |
| Frontend contracts | `AscendantEmpire.FrontendContracts/MatchMaking/` | `IMatchMakingGameCollection`, `IMatchMakingGame`, `IMatchMakingSlot`, `IPlayerDefinition`, `IUsedFactionColour`. |
| DTOs | `AscendantEmpire.Application/Features/MatchMaking/Classes/` | `DTMatchMakingGameCollection` etc. — the serialized shapes. |

Handlers are auto-registered by MediatR's assembly scan, and their validators by the existing
validation pipeline — no DI wiring was added.

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
| Main component | `src/features/matchmaker/components/MatchMaker.tsx` | Lists games/slots, claims a free slot, "Play now" / "Edit" on a held slot. |
| Setup dialog | `src/features/matchmaker/components/DefinePlayerDialog.tsx` | Two-page `PagedDialog` for the ruler and faction; fetches and saves its own definition. |
| API calls | `src/features/matchmaker/api/matchMakingApi.ts` | `getMatchMakingGames()`, `claimPlayerSlot()`, `getPlayerDefinition()`, `updatePlayerDefinition()`. |
| Types | `src/features/matchmaker/types/` | Camel-cased mirrors of the frontend contracts, plus `RulerGender.ts` (the gender union and the three ruler-title lists). |
| Landing link | `index.html` | Adds a "Match Maker" link next to Game / Map Editor. |

Two shared controls were added for this and live in the UI library rather than the feature, because
nothing about them is matchmaking-specific:

| Piece | Location | Role |
|---|---|---|
| `FreeTextComboBox` | `src/components/ui/FreeTextComboBox.tsx` | A controlled text field with a suggestion dropdown. Unlike `SearchableComboBox` — a *picker* that owns its text — the caller owns the value and free text always wins. |
| `DistinctColors` | `src/components/ui/colorpicker/distinctColors.ts` | 24 Tailwind swatches chosen so no two can be confused. See the note in that file for how the selection was measured, and `__tests__/distinctColors.test.ts` for the invariant that keeps it true. |

`FieldRow`, `inputClass` and `errorClass` also moved up into `src/components/ui/` in the same pass:
they had been living in the entitysetuptool feature, and a player-facing dialog reaching into a dev
tool's internals for a layout primitive is the wrong dependency direction.

### Flow

1. Open `/matchmaker/`. With no token, `AuthGate` shows the existing login / register form.
2. Once logged in, `MatchMaker` lists joinable games. Each slot shows a dot in its faction colour
   (hollow while nobody has picked one) and is one of:
   - **Your slot** — badge + **Edit** + **Play now**.
   - **Taken** — a muted badge (no name shown).
   - **Free** — a **Claim** button (disabled if you already hold a slot in that game).
3. **Claim** calls `POST /match-making/claim`, refetches the list so the seat flips to "your slot",
   and then opens `DefinePlayerDialog` on it.
4. The dialog fetches `GET .../my-player` on open, and saves with `PUT .../my-player`:
   - **Ruler** page — Ruler Name, Ruler Gender, Ruler Title, *in that order*: the gender decides
     which titles the combo box suggests, so it has to be answered first. The title is free text and
     may be left blank.
   - **Faction** page — Faction Name and a `ColorPicker` over `DistinctColors`, with the colours
     rivals already hold struck through. A save that loses the race anyway comes back as a 409 and is
     shown against the colour field, with the page switched to it.
   - **Cancel** keeps the slot, carrying whatever the game seeded it with. That is only reasonable
     because of the **Edit** button, which reopens the same dialog — the two are one guarantee.
5. **Play now** calls `enterGame(gameId)` (from the shared auth API) to swap the user token for a
   game token, then redirects to `/game/`.

Auth reuse is deliberate: registration, login, the token store (`src/api/authToken.ts`), and the
`Authorization` header (`src/api/http.ts`) are all the existing auth plumbing — the Match Maker
just adds the claim step on top.

---

## Limits & follow-ups

- **No release / leave** — you can claim a slot but not give it back. Releasing would need a
  guarded endpoint (only the holder; consider what happens to in-progress game state). Note that a
  released slot would keep occupying its faction colour unless that path also clears
  `FactionColour`, because the unique index is on the colour rather than on the claim.
- **Claimants are hidden** — taken slots don't show who holds them. Surfacing usernames (or
  display names) would be a small contract addition if a lobby view wants it.
- **`/games/open` is unchanged** — it stays anonymous and only lists `Active` players. The Match
  Maker uses its own authorised listing so it can compute the per-user claim flags.
- **Slot identity is now the player's** — a seeded slot carries only placeholders (`"Nobody"`,
  `"Player 3"`, `"Faction 3"`, no gender, no colour), and the two `my-player` endpoints above let
  the claimant replace them. Uniqueness is enforced on the colour only: two factions in one game may
  still share a ruler name or a faction name.
- **Colour uniqueness is exact-value, not perceptual** — the index stops two players holding the
  same hex, but nothing stops them picking two colours that merely look alike. The defence is the
  palette the picker offers, not a server rule.
