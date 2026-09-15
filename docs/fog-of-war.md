# Fog of War

How what a player *knows* about the campaign map reaches the screen, and why it is two things rather
than one.

The rules that decide what a Force or Building can see — sight range, elevation, sight-blocking
biomes — are design, and live in [Vision on the campaign map](design/campaign-map.md#vision-on-the-campaign-map).
This document is the pipeline: the storage behind it, the shape it crosses the API in, and how the
client draws it.

## The two states

Every tile is in exactly one of three states for a given player, out of two independent facts.

| | Explored | Watched | Drawn as |
|---|---|---|---|
| **Unexplored** | no | no | Solid fog. Near-opaque; there is no real terrain underneath to show. |
| **Explored, unwatched** | yes | no | Light haze over the real biome colour. Terrain visible, occupants not drawn. |
| **Watched** | yes | yes | No overlay. Terrain, structures and forces all drawn. |

The fourth combination — watched but unexplored — cannot happen: looking at a tile is what explores
it.

**Explored is permanent.** Once a player has seen a tile they know its biome and geography for good,
and nothing takes that back. It accumulates over a game and never shrinks.

**Watched is live, and shrinks.** It is true only while one of the player's own forces or structures
can see the tile this instant. Walk the force away and the tile stops being watched the same turn.

The distinction is the whole point. Terrain is a fact about the ground and survives losing sight of
it; **structures and forces are intel** and do not. An explored tile that nobody is watching shows
the player what the land looks like and tells them nothing about who is standing on it.

## Storage

`game.PlayerCampaignMap` holds one row per (player, tile) — see
[Player Campaign Map](campaign-and-battle-maps.md#player-campaign-map). The rows are created for the
**whole map** when the game is set up, with `BiomeTypeId = 0` (`Unknown`) and both turn columns
`NULL`; they are not inserted as ground is discovered.

That makes `FirstSeenOnTurnId` the exploration flag:

| Column | Meaning |
|---|---|
| `FirstSeenOnTurnId` | `NULL` until the player first sees the tile; the `turns.PlayerTurn` they did, thereafter. **This is `IsExplored`.** |
| `LastSeenOnTurnId` | The `turns.PlayerTurn` of the most recent sighting, stamped every time the tile is seen again. Not yet used by the client. |
| `BiomeTypeId`, `CampaignMapGeographyTypeId` | Copied from the world map when the tile is first seen. `Unknown` until then. |

There is no stored "watched" column, and there should not be: it is derived from where the player's
observers are standing at the moment of the read.

## The read

`GetPlayerCampaignMapHandler` is the only read that turns sight into a player's map. The sight rules themselves live in one pure class, `FieldOfView`, reached through `IFieldOfViewService`; the intelligence witness service uses the same service, so live vision and intelligence cannot drift apart either. The handler owns only the exploration stamp, the lookup of what is standing on the seen tiles, and the response.

Both endpoints that describe a player's view go through the handler, so the two cannot drift:

* `POST /campaign-map/player` — the map read, sent with `loadWholeMap: true` by the game UI.
* `POST /force-orders/step` — a force step, which re-runs the same read (with `LoadWholeMap: false`)
  after the move is committed and returns the result alongside the new position.

The read has a side effect, and it is load-bearing: it stamps `FirstSeenOnTurnId` on every tile the
observers can see, *before* it builds the response. Exploring the map is something the player does by
looking at it.

### The response shape

Both endpoints answer with a list of **observers**, each carrying the tiles it can see:

```
observers: [
  { id, observerRecordType, xCoord, yCoord, seenTiles: [ tile, … ] },
  …
]
```

A tile carries `isExplored` alongside its biome, geography, structures and forces.

Two kinds of observer come back, told apart by `observerRecordType` (`RecordType` on the API):

| `observerRecordType` | What it is |
|---|---|
| `1` Unit, `2` Command, `3` Structure | A **real observer** — something of the player's that is looking at those tiles. Its tiles are watched, and always `isExplored: true`. |
| `0` None | The **whole-map pseudo-observer**. Stands nowhere, watches nothing, and exists only to carry remembered terrain in the same shape. Sent only when `loadWholeMap` is true. |

The pseudo-observer carries every tile the real observers did *not* cover — which, because the
player's map has a row for the whole board, means both the ground they remember (`isExplored: true`,
with real terrain and empty `structures`/`forces`) and the ground they have never been to
(`isExplored: false`, terrain still `Unknown`). It is the only place in any response where
`isExplored` is ever false.

> Treating the pseudo-observer's tiles as watched lights the entire explored map and collapses the
> two layers back into one. `useFogOfWar` filters on `observerRecordType` for exactly this reason.

## The client

```
POST /campaign-map/player ─┐
                           ├─► tiles cache (React Query: ["playerCampaignTiles", gameId, playerId])
POST /force-orders/step ───┘         │
                                     ├─ tiles[]      ─► buildTiles2d ─► TileCell.isExplored ─┐
                                     │                                                        ├─► getTileBackground
                                     └─ observers[]  ─► useFogOfWar ─► observedTileKeys ─────┘
```

One cache holds both halves, and both endpoints write to it. That is deliberate: the fog has a single
source of truth, so the map read and a force step can never each hold their own idea of what the
player can see.

The two halves merge by **opposite** rules, in `mergeTiles`:

* **Tiles accumulate.** A response describes only what it knows about; every other cached tile is left
  exactly as it was, object identity included — which is also what keeps `React.memo(Tile)` from
  re-rendering the whole grid after a one-tile move. Explored ground therefore builds up across a
  game.
* **Observers are replaced** whenever a response carries any. They are the complete picture of what is
  being watched right now, so a tile absent from the new set is one nothing is looking at any more,
  and it drops back to haze.

`isExplored` rides on the tile rather than living in a set, because it changes only when the tile's
DTO changes; `observedTileKeys` is a set rebuilt from the observers, memoised so the whole grid does
not re-render when something unrelated on the page does.

### Where each piece lives

| File | Role |
|---|---|
| `features/map/api/playerCampaignMapApi.ts` | Wire types and normalisation. `isExplored` defaults to **false** when absent. |
| `features/map/hooks/useCampaignMapTiles.ts` | The cache and `mergeTiles` — the accumulate/replace rules above. |
| `features/game/hooks/useForceStep.ts` | Writes a step's tiles *and* observers into the same cache. |
| `features/game/hooks/useFogOfWar.ts` | Derives `observedTileKeys`, skipping the whole-map pseudo-observer. |
| `features/game/hooks/useTileRendering.ts` | `getTileBackground` — picks one of the three overlays. |
| `features/map/components/Tile.tsx` | Draws structures and forces only when `isObserved`. |

### Occupants are not drawn on unwatched ground

The cache keeps the last-known contents of a tile, and the server stops reporting them once nothing is
watching. Drawing them anyway would leave an enemy force painted on ground the player cannot see, long
after it had marched off. `Tile` therefore skips structures and forces entirely when the tile is not
observed. Its `isObserved` prop defaults to `true`, so a grid with no fog of war — the map editor's —
is unaffected.

The player's own forces are never hidden by this: a force always sees the tile it occupies.

## The bug this shape exists to prevent

Before F-11 the client had one boolean where the model has two facts. Fog was drawn from the observer
set alone, and that set was owned by the *vision dev tool*:

* Opening a game never populated it, so the entire map rendered grey however much had been explored.
  It only un-greyed if the player poked the dev tool.
* A force step returns live sight only. Replacing the set with it — correct on its own terms — greyed
  out every explored tile except the eight around the force that had just moved.

Both symptoms are the same mistake: using "can I see this right now" to answer "have I ever been
here". Keeping the two apart, all the way from `FirstSeenOnTurnId` to the overlay, is what this
document is describing.

## Known gaps

* **The stamp is still a side effect of the *read*, not of movement.** A tile is stamped the next time its owner opens the map, not when a unit walks past. The witness service extraction (F-23) moved the sight rules out of the read but deliberately left the stamp behind: relocating it onto the movement path changes when ground becomes explored, and is its own piece of work. See [intelligence-and-sightings.md](design/intelligence-and-sightings.md).
* **`Vision cleared` is asymmetric.** The dev tool's clear wipes the player's map server-side and the
  refetch brings back a fully unexplored board. That is the intent, but it is a developer action and
  has no in-game equivalent.
