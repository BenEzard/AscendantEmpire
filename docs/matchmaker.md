# Match Maker

The Match Maker lets a person create an account, log in, **set a game up** and **claim a player slot**
in an open game — closing the "linking users to players" gap called out in
[authentication-and-authorisation.md](authentication-and-authorisation.md). Claiming a slot and then
defining the ruler and faction in it is what fills a lobby; once every player has done both, the game
starts and they can jump in.

Creating a game belongs here because the slots it is created with are exactly what this screen hands
out: setting a game up, filling it and entering it are one screen rather than three.

This document covers the concept, the backend endpoints, the frontend subsystem, and how it fits
with the existing auth flow.

---

## Concept

A **player slot** is just a `game.Player` row. When a game is set up,
[`GameSetupService`](../AscendantEmpire.Application/Features/Game/Services/GameSetupService.cs)
creates one `Player` per seat with `UserId = null` (unassigned) and `Status = Active`.

- **Claiming** a slot sets that `Player.UserId` to the logged-in account.
- **Defining** a player writes the ruler and faction identity into that same row. A completed
  definition is a non-null `Player.RulerGender` — a seeded slot deliberately asserts none, which is
  what makes "has this player been through setup?" answerable without a second flag.
- A user may hold **at most one slot per game**, enforced by the filtered unique index
  `UX_Player_Game_User` on `game.Player(GameId, UserId)`. The explicit checks in the handler turn
  a lost race into a friendly error; the index is the real guarantee.

Claiming does **not** change the auth model — it just fills in the `UserId` that
`POST /auth/enter-game` later looks for. Before this feature you had to assign players with raw
SQL; now the Match Maker does it.

```
   log in        set a game up          claim a slot        define yourself        enter the game
   (user token)  (empty slots)          (Player.UserId)     (the identity)         (game token)     play
   AuthGate ───▶ POST /games/setup ───▶ POST .../claim ───▶ PUT .../my-player ───▶ POST /auth/enter-game ───▶ /game/
                                                                    │
                                                  the last one of these starts the game
```

### When a game starts

A game leaves the lobby when **every active slot has a user and a completed definition** — so the
last player to define themselves is what starts it, not the last seat to be claimed. A claimed seat
nobody has filled the form in for has no identity to play as, so a full lobby can still be
`AwaitingPlayers`.

The trigger lives in `UpdatePlayerDefinitionHandler`, which wraps the save and
`ICampaignRoundService.StartGameAsync` in one transaction: a start that failed must not leave the
game ready and never begun. `StartGameAsync` is gated on `AwaitingPlayers` and idempotent, so a
player re-saving their definition after play has begun cannot start it twice.

### Unique within a game

Seven values of a definition belong to one player for the life of the game, each with a unique index
on `game.Player` behind it:

| Value | Index | Notes |
|---|---|---|
| `RulerName` | `UX_Player_Game_RulerName` | |
| `FactionName` | `UX_Player_Game_FactionName` | |
| `PeopleName` | `UX_Player_Game_PeopleName` | |
| `Demonym` | `UX_Player_Game_Demonym` | |
| `CultureName` | `UX_Player_Game_CultureName` | |
| `FactionColour` | `UX_Player_Game_FactionColour` | Filtered: `NULL` means "not picked yet" |
| `RulerPortraitPath` | `UX_Player_Game_RulerPortraitPath` | Filtered: `''` means "not chosen yet" |

`Language` is deliberately **exempt** — two peoples sharing a language is a legitimate thing for a
player to express, and nothing attributes anything by language.

The five names compare **case-insensitively**, which rests on `Latin1_General_CI_AS` being pinned on
those columns via `Player.IDENTITY_COLLATION`. Nothing else in the solution sets a collation, so
without that the rule would mean whatever the server that created the database was configured for.
Spacing is not something a collation settles, so the handler also collapses runs of internal
whitespace before it stores a name: `"The  Vashti"` and `"The Vashti"` are one name. The stored value
keeps the casing the player typed, which is what gets displayed — there are no normalised sibling
columns.

### Locked once play begins

From `GamePlayStatus.InProgress` onward, **only `RulerTitle` stays editable**. A title is expected to
change as an empire grows; the names around it are what everything else labels and attributes by.

The other values are **ignored rather than refused**, because the client round-trips the whole
definition and renders those fields disabled — refusing would turn a legal title edit into an error
over values the caller only echoed back. A blank title still lands as
`Player.DEFAULT_RULER_TITLE`, so clearing it mid-game is a legal edit.

The start gate is what makes the lock safe to apply unconditionally: every player reaching
`InProgress` has a real definition, so there is no undefined slot left to make an exception for.

---

## API endpoints

All live on
[`MatchMakingController`](../AscendantEmpire.Api/Controllers/MatchMakingController.cs). The whole
controller is `[Authorize]`, so a **user token is required**. The account is always read from the
token (`User.GetUserId()`), never from the request body.

### `GET /match-making/games`

Lists the joinable games and every **active** slot in each, flagged as claimed or free.

A game is listed once it has finished setup and has not ended
(`CreationStatus == SetupComplete && PlayStatus is AwaitingPlayers or InProgress`) — a started game
has to stay in the list so the people in it can get back to it. Both claimed and unclaimed slots are
returned so the UI can show taken vs free seats.

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
      "hasStarted": false,
      "slots": [
        { "playerId": 70, "rulerTitle": "King", "rulerName": "Player 1", "factionName": "Faction 1",
          "factionColour": "#0069a8",
          "rulerLabel": "King Player 1 (Faction 1)", "isClaimed": true,  "isClaimedByCurrentUser": true,
          "isDefined": true },
        { "playerId": 71, "rulerTitle": "Queen", "rulerName": "Player 2", "factionName": "Faction 2",
          "factionColour": "#c10007",
          "rulerLabel": "Queen Player 2 (Faction 2)", "isClaimed": true,  "isClaimedByCurrentUser": false,
          "isDefined": true },
        { "playerId": 72, "rulerTitle": "Nobody", "rulerName": "Player 3", "factionName": "Faction 3",
          "factionColour": null,
          "rulerLabel": "Nobody Player 3 (Faction 3)", "isClaimed": false, "isClaimedByCurrentUser": false,
          "isDefined": false }
      ]
    }
  ]
}
```

- `isClaimed` — some account holds the slot; it can't be claimed. The claiming user's identity is
  **not** revealed.
- `isClaimedByCurrentUser` — the requesting account holds this slot (UI shows it as "your slot").
- `isDefined` — the player in the slot has been through setup, so the names and colour above are
  theirs rather than seeded placeholders. Always `false` on an unclaimed slot. This is the other half
  of the start condition: a lobby with no free seats but an undefined slot is still waiting, and the
  UI says "*n* still setting up" rather than only that the game has not started.
- `hasStarted` — `PlayStatus == InProgress`, the only state `POST /auth/enter-game` accepts. It can
  be `false` on a lobby that is full, which is why `isDefined` exists.
- `factionColour` — the colour the slot's player picked, or `null` while nobody has. `rulerGender`
  is deliberately **not** here: nothing renders it, and it is the one identity value that isn't
  obviously public. `isDefined` carries the only thing the lobby needs from it.
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

Claiming a seat does **not** start the game, not even the last free one — see
[When a game starts](#when-a-game-starts).

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
  "peopleName": "People 3",
  "demonym": "Person 3",
  "cultureName": "Culture 3",
  "language": "Language 3",
  "rulerPortraitPath": "",          // "" = no portrait chosen; the client draws a crown
  "usedFactionColours": [
    { "value": "#0069a8", "usedBy": "Faction 1" },
    { "value": "#c10007", "usedBy": "Faction 2" }
  ],
  "usedRulerPortraits": [
    { "value": "/portraits/04_egyptian_queen.png", "usedBy": "Faction 1" }
  ],
  "isLocked": false              // true once play has begun; only rulerTitle is then editable
}
```

`isLocked` mirrors the lock `PUT` applies, so the dialog can render those fields disabled rather than
let a player edit them and find out on submit. It is read off the game's `PlayStatus`; the `PUT`
handler is what enforces it.

`usedRulerPortraits` lists only portraits a rival has actually chosen — a slot still on the default holds
none, which is what lets every undecided player share that state — and never the caller's own, which must
stay pickable.

`usedFactionColours` is taken here rather than derived from the games listing the client already
holds: that listing is cached, and a stale reading would offer a colour that has since been taken.
It never includes the caller's own colour, which must stay pickable.

### `PUT /match-making/games/{gameId}/my-player`

Defines the caller's own ruler and faction. A full replacement of all ten values, not a patch.

Gated on `JoinGame`, the same permission as claiming. The body carries **no** identity at all — the
game comes from the route and the slot from the token — so it cannot name a slot the caller does not
own.

```jsonc
// request
{ "rulerTitle": "Empress", "rulerName": "Aelara", "rulerGender": "Female",
  "factionName": "The Ember Pact", "factionColour": "#c800de",
  "peopleName": "The Vashti", "demonym": "Vashti",
  "cultureName": "Vashti Highland", "language": "Old Vashti",
  "rulerPortraitPath": "/portraits/07_northern_queen.png" }
// response: 204 No Content
```

| Situation | Result |
|---|---|
| Saved | `204 No Content` |
| Validation failed (see below) | `400` |
| Missing / invalid token | `401` |
| Token lacks `JoinGame` | `403` |
| Caller controls no player in that game | `404` |
| Another player in that game already holds one or more of the unique values | `409` |

A `409` carries a `detail` naming **every** clashing field at once, so a player who picks three taken
values is told all three rather than resubmitting three times:

```jsonc
// 409
{
  "errors": [
    "That people name has already been taken by another player in this game.",
    "That colour has already been taken by another player in this game."
  ],
  "detail": {
    "fields": [
      { "field": "peopleName",    "message": "That people name has already been taken by another player in this game." },
      { "field": "factionColour", "message": "That colour has already been taken by another player in this game." }
    ]
  }
}
```

Each `field` is spelled as the request body spells it, so the client keys straight off the value it
sent. A `409` with **no** `detail` means the race was lost at the index rather than at the handler's
own check — it cannot say which value it was, so the client shows that message in the banner.

- **`rulerTitle` is optional.** Blank (or omitted) is a valid choice and is stored as `"Nobody"`,
  the same placeholder `GameSetupService` seeds a slot with. The substitution happens in the
  handler, not as a database default, so there is one place that decides it. It is also the only
  field that survives the lock — see [Locked once play begins](#locked-once-play-begins).
- **`rulerGender` is required** and must be `"Male"` or `"Female"`. The enum starts at 1, so an
  omitted value fails validation rather than quietly arriving as `Male`.
- **`factionColour` is required** and must match `^#[0-9a-fA-F]{6}$`. It is stored lower-cased, and
  is unique within the game — enforced by the filtered unique index `UX_Player_Game_FactionColour`
  on `game.Player(GameId, FactionColour)`, with an up-front check in the handler that turns the
  common case into a friendly `409`.
- The colour is **not** checked against the frontend's palette. Doing so would mean keeping a copy
  of that palette in C# forever, and the worst a hostile client achieves by sending something else
  is an ugly colour.
- **`peopleName`, `demonym`, `cultureName` and `language` are all required**, each capped at 50
  characters. They are free text the player authors — nothing is looked up by them — but the first
  three are **unique within the game**, as are `rulerName` and `factionName`. `language` is the one
  exemption. See [Unique within a game](#unique-within-a-game). `GameSetupService` seeds each with a
  placeholder, so a slot never reads as blank in the lobby before it is claimed.
- The five unique names are stored **trimmed and with internal whitespace collapsed**, so
  `"The  Vashti"` cannot sit alongside `"The Vashti"` as a second name. `language` is trimmed only.
- **`rulerPortraitPath` is optional**, like the ruler title. Blank (or omitted) is a valid choice and is
  stored as the empty string, which is what `GameSetupService` seeds every slot with. The client draws a
  crown for it. A value that *is* given is capped at 100 characters and must match
  `^/portraits/[a-z0-9_-]+\.png$` — a root-relative path to a file in the UI's `public/portraits/` folder,
  a client path the API never resolves, the same arrangement as `Asset.IconPath`.
- **A chosen portrait is unique within the game.** Two players may not wear the same face, so a non-blank
  value another slot in the same game already holds comes back as a `409`. Enforced by
  `UX_Player_Game_RulerPortraitPath`, filtered to exempt the empty default: it is the one value the
  column may hold more than once, because any number of players can be undecided at the same time.
- The path is checked for **shape but not membership**: the twenty portraits are not enumerated in
  C#, for the same reason the colour is not checked against the palette. The shape rule is there
  because this value — unlike a name — is rendered into an `<img src>` that every other player in
  the game sees in their toolbar, so an off-site URL would pull a remote image onto their screen.

---

## Backend components

| Piece | Location | Role |
|---|---|---|
| `MatchMakingController` | `AscendantEmpire.Api/Controllers/MatchMakingController.cs` | `[Authorize]` endpoints; reads the account from the token. |
| `GetMatchMakingGamesHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Lists joinable games + slots, deriving the claim flags per slot. |
| `ClaimPlayerSlotHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Assigns `Player.UserId`; guards "already taken" / "already in this game". Does not start the game. |
| `GetPlayerDefinitionHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Reads the caller's own slot plus the rivals' colours, in one query, and whether it is locked. |
| `UpdatePlayerDefinitionHandler` | `AscendantEmpire.Application/Features/MatchMaking/Handlers/` | Writes the ten identity values; defaults a blank title, aggregates the seven uniqueness clashes, applies the lock, and starts the game on the last definition. |
| Backend contracts | `AscendantEmpire.BackendContracts/MatchMaking/` | `MatchMakingGame`, `MatchMakingSlot`, `PlayerDefinition`, `UsedFactionColour`, `PlayerDefinitionConflict` (handler output); `ClaimPlayerSlotDto`, `UpdatePlayerDefinitionDto` (request bodies). |
| Frontend contracts | `AscendantEmpire.FrontendContracts/MatchMaking/` | `IMatchMakingGameCollection`, `IMatchMakingGame`, `IMatchMakingSlot`, `IPlayerDefinition`, `IUsedFactionColour`. |
| DTOs | `AscendantEmpire.Application/Features/MatchMaking/Classes/` | `DTMatchMakingGameCollection` etc. — the serialized shapes. |

Handlers are auto-registered by the mediator's assembly scan, and their validators by the existing
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
| Main component | `src/features/matchmaker/components/MatchMaker.tsx` | Creates a game, lists games/slots, claims a free slot, "Play now" / "Edit" on a held slot. |
| Setup dialog | `src/features/matchmaker/components/DefinePlayerDialog.tsx` | Two-page `PagedDialog` for the ruler and faction; fetches and saves its own definition. |
| New game dialog | `src/features/matchmaker/components/NewGameDialog.tsx` | Two-page `PagedDialog` for the game's name, player count, battle map size and seed, plus the campaign map it is played on; posts `/games/setup`. |
| API calls | `src/features/matchmaker/api/matchMakingApi.ts` | `getMatchMakingGames()`, `claimPlayerSlot()`, `getPlayerDefinition()`, `updatePlayerDefinition()`. |
| New game settings | `src/features/matchmaker/api/gameSettingsApi.ts` + `hooks/useNewGameSettings.ts` | `GET /games/settings`, cached indefinitely and not requested until the dialog is first opened. The bounds the new game form is built from, so it never hardcodes one the save would refuse. |
| Types | `src/features/matchmaker/types/` | Camel-cased mirrors of the frontend contracts, plus `RulerGender.ts` (the gender union and the three ruler-title lists) and `RulerPortrait.ts` (the twenty portraits, each with its file path, display name and the gender it reads as). |
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
   - **Your slot** — badge + **Edit** + **Play now**. **Play now** is disabled until the game has
     started, with a tooltip saying what it is waiting for.
   - **Taken** — a muted badge (no name shown), or **Setting up** when that player has not been
     through the dialog yet, since that seat is what the whole game is waiting on.
   - **Free** — a **Claim** button (disabled if you already hold a slot in that game).

   The card's header says `n/m slots taken`, then either `• n still setting up` or
   `• waiting to start` — naming which of the two waits the lobby is actually in rather than only
   that it has not started.
3. **New Game**, beside the filter row's **Refresh**, opens `NewGameDialog` — the game's name, player
   count, battle map size and seed on one page, the campaign map it is played on the other. Creating
   posts `/games/setup` (authorised on the `CreateGame` permission) and refetches the listing, so the
   seats the new game was created with are claimable without a manual refresh.
4. **Claim** calls `POST /match-making/claim`, refetches the list so the seat flips to "your slot",
   and then opens `DefinePlayerDialog` on it.
5. The dialog fetches `GET .../my-player` on open, and saves with `PUT .../my-player`:
   - **Ruler** page — Ruler Name, Ruler Gender, Ruler Title, Ruler Portrait, *in that order*: the
     gender decides both which titles the combo box suggests and which faces the portrait gallery
     offers, so it comes first. It opens on `Male` for a slot the server holds no gender for, and
     carries no validation — the radio pair offers no way back to none. The title is free text and
     may be left blank. The portrait is an `ImagePicker` — a thumbnail of the current choice that
     opens a grid of the chosen gender's portraits from the UI's `public/portraits/`, with the
     portraits rivals already wear struck through and unpickable. Switching gender drops a held
     portrait belonging to the gender just left, rather than leaving the trigger showing a face the
     grid no longer offers. It may be left unchosen, in which case the trigger shows a crown.
   - **Faction** page — Faction Name, a `ColorPicker` over `DistinctColors` with the colours rivals
     already hold struck through, and then the four names for the people behind the faction: People
     Name, Demonym, Culture Name and Language.
   - **Clashes** — a `409` names every field it refused, and each message is shown against its own
     field, with the page switched to whichever one holds a clash (Ruler wins ties). Editing a field
     clears the message reported against it. A `409` the server could not attribute goes in the
     dialog's banner instead, so the refusal is never swallowed.
   - **A game under way** renders every field but the title disabled, with a banner explaining why,
     and skips the validation those fields would otherwise be held to — so a title edit still saves
     on a slot whose stored values predate the people-and-culture columns.
   - **Cancel** keeps the slot, carrying whatever the game seeded it with. That is only reasonable
     because of the **Edit** button, which reopens the same dialog — the two are one guarantee. Note
     that a slot left undefined holds the whole game up, since the last definition is what starts it.
6. **Play now** calls `enterGame(gameId)` (from the shared auth API) to swap the user token for a
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
- **Slot identity is the player's** — a seeded slot carries only placeholders (`"Nobody"`,
  `"Player 3"`, `"Faction 3"`, `"People 3"`, `"Person 3"`, `"Culture 3"`, `"Language 3"`, no portrait,
  no gender, no colour), and the two `my-player` endpoints above let the claimant replace them once,
  before play begins. Each placeholder is numbered per slot precisely because the real values are
  unique within the game — see [Unique within a game](#unique-within-a-game).
- **No release / leave, and no rename either** — the lock means a mid-game change of heart has no
  path at all beyond the title. That is the point: the names are what the Assets work and anything
  else that labels or attributes by a player reads, and a rename mid-game would make those
  unreadable. If a rename is ever wanted, it has to be a deliberate feature that also decides what
  happens to everything already labelled.
- **Colour uniqueness is exact-value, not perceptual** — the index stops two players holding the
  same hex, but nothing stops them picking two colours that merely look alike. The defence is the
  palette the picker offers, not a server rule.
