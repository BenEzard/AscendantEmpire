# Experience points

How experience reaches a Player's technologies: who earns it, where it is held, and when it is spent. The catalogue of experience kinds, and the three-bucket research model this feeds, are in [technology.md](technology.md).

## Intent

The experience bucket of the three-bucket research model has four grant rules covering exploration, movement and settling, and one distributor that every future producer shares.

The four rules are deliberately mundane. The point of the slice is not the rules — it is that `PendingExperienceAccrual` has a writer and a reader, so that extraction, refining, manufacture and husbandry have somewhere to plug into when they land.

### The four grant rules

| Rule | Experience type | Points | Earned when |
| --- | --- | --- | --- |
| First sighting | `Exploration` | 3 per tile | The Player is the first in the Game to see the tile. |
| Later sighting | `Exploration` | 1 per tile | The Player sees a tile for the first time, but somebody else saw it first. |
| Treading new ground | `Biome.<Name>` | 1 per tile | A Force of the Player's enters a tile that carries no standing Structure. At most once per tile per Turn, however many Forces cross it and however often. |
| Settling | `Biome.<Name>` | 10 | A Structure that a Settlement hangs off reaches 100%. |

## Goals

- Give `PendingExperienceAccrual` exactly one writer, so every later producer has one path to copy rather than a choice to make.
- Give it exactly one reader, which splits each type's total across the technologies that consume it, and accounts for what could not be used.
- Keep the four rules out of the systems they observe. Movement and settling should not learn that the technology schema exists.
- Stay within the existing schema. No migration.

## Non-goals

- **No per-type, per-Round, per-Player history.** `ResearchHistory` already carries per-Round totals by bucket, and that is the reporting grain for now.
- **No scaled storage.** Experience points stay `int`, matching the other two buckets and the columns `ResearchHistory` and `PlayerTechnology` already have.
- **No change to the seeded catalogue.** The rules resolve against `Exploration` and `Biome.<Name>` as seeded. No `Exploration.<Biome>` codes, and no parent or fallback link on `ExperienceType`.
- **No `broader.focused` resolution.** Dotted codes stay a naming convention. The 20% consolation for a missing focused code is not implemented, and no rule here needs it.
- **No instant feedback.** A point earned mid-Turn changes nothing a Player can see until their end-of-Turn step runs.
- **No new API or UI surface.** Bucket progress is already served by `GetPlayerResearchHandler` and rendered by the Science Advisor, so it becomes visible for free. `PlayerUnusedExperience` gains no endpoint here; it gets one when something compares Players on it.
- No overflow of experience onto a second technology, and no backfill of experience onto a technology discovered later. Both are already settled in [technology.md](technology.md).

## Anti-goals

Things that would make this a failure even if it worked:

- **Points that vanish.** A rule that earns a point which never reaches a technology or `PlayerUnusedExperience` is worse than a rule that earns nothing, because nobody can tell the difference by looking.
- **Double counting.** A Player who opens their map view three times, or whose end-of-Turn pipeline re-runs, must earn the same points as one who does neither.
- **An exploitable loop.** A Force walking back and forth across one tile must not farm experience, which is why the movement rule counts distinct tiles per Turn rather than steps.
- **Distribution that depends on event order.** Ten jungle tiles must distribute identically however the Turn's events interleaved.

## Constraints

- **The map sighting stamp is a side effect of a read.** `GetPlayerCampaignMapHandler` sets `PlayerCampaignMap.FirstSeenOnTurnId` and `CampaignMap.FirstSeenByPlayerId` while serving a `GET`. A Player who never opens their map view earns no exploration experience. This is pre-existing, already carries a `TODO` in that handler, and is not fixed here.
- **That stamp carries the reader's own most recent Turn, not the open one.** `CampaignTurnService.GetObservationTurnIdAsync` resolves the reader's latest `PlayerTurn`, which is pinned by `GetObservationTurnIdTests`. A Player viewing their map while waiting for their next Turn therefore stamps tiles with the Turn whose end-of-Turn pipeline has already run. Any rule that derives exploration from "tiles stamped with this Turn" loses those tiles silently. This constraint is what decides the approach below.
- **The even split needs one settled recipient set.** `PendingExperienceAccrual`'s own remarks make the case: allocating as each event arrives means a technology discovered halfway through a Turn changes the divisor. One split per Turn against one snapshot is the only deterministic option.
- **`ApplyResearch` already owns discovery.** It decides the clamp, the status and the two discovery stamps together, guarded by `PlayerResearchState.LastResearchAppliedOnRound`. A second step that could also discover a technology would be a second owner of that decision.
- **Experience is distributed before directed points are spent**, per the turn order in [technology.md](technology.md).

## Approaches

### A — one end-of-Turn derivation step

All four rules computed in a single new pipeline step, from the stamps and history rows the Game already writes, with no producer call sites at all. Every rule lives in one file and nothing existing is touched.

It fails on the two exploration rules. Because the sighting stamp carries the reader's own most recent Turn, tiles discovered while viewing the map during somebody else's Turn are stamped with a Turn that has already been processed, and a step filtering on the current Turn never sees them. Rescuing them needs a watermark or a "paid" marker on `PlayerCampaignMap` — new per-tile state whose only job is to compensate for when the stamp happens, and which extraction and manufacture could not reuse, because those genuinely accrue mid-Turn.

### B — one allocator, called where the fact is created (chosen)

A single `IExperienceAllocator` upserts `PendingExperienceAccrual`. Exploration calls it from the map read, where the two discovery counts are already computed. Movement and settling are derived in a batch at end of Turn, from `MovementHistory` and `StructureHistory`, so neither system gains a call site. `ApplyResearch` drains the table and distributes.

No watermark is needed: the accrual is written at the moment the fact is, and drained by whichever distribution runs next, so a late map read simply pays the following Round. The allocator is also the piece every future producer needs regardless, which makes it the part of this slice with the longest life.

The cost is that exploration experience stays coupled to the map read, and the rules end up in two places rather than one.

### C — pure producer-side, movement included

As B, but movement calls the allocator on each step rather than being derived. Rejected: it would need its own per-Turn, per-tile dedup state, which `MovementHistory` already holds, and writing per step works against collating accruals into one write.

**B is chosen.** A's watermark buys nothing but file tidiness, and buys it with state no later producer can share.

## How it works

### The allocator

One service, the only thing that writes `PendingExperienceAccrual`.

It takes a Game, a Player and a batch of (experience type code, points) pairs, and increments one row per code, inserting where no row exists. Callers hand it a whole batch rather than calling per event, which is what keeps a Force crossing ten jungle tiles to one write.

Codes are resolved to `ExperienceType.Id` by one query per batch, so a caller names `"Biome.Jungle"` and never holds an id. **An unrecognised code refuses the whole batch, rather than dropping the one entry**: a typo that silently swallowed points would be invisible, and the anti-goals above rule that out. That the catalogue and `ExperienceTypeCodes` agree is held by a test, because the warning a refusal leaves is not loud enough to catch a missing seed row in play.

**Resolution happens before the caller writes anything.** At the map read the sighting stamp is the only record that a tile was ever new, so a stamp that commits without its accrual loses those points permanently. The allocator therefore resolves codes up front, which turns a catalogue gap into a refusal before the stamp rather than after it, and the accrual is written on the same context and transaction as `UpdateCampaignMapFirstSeenAsync` so the stamp and the points commit together. That means folding that handler's two parallel single-context writes into one transaction.

`BiomeType` maps to a code by name — `BiomeType.Jungle` to `"Biome.Jungle"` — matching how `ExperienceTypeSeedData` authors them. `Unknown` and `Void` have no code and accrue nothing, which is correct: they are the absence of terrain rather than a kind of it.

### Exploration, at the map read

`GetPlayerCampaignMapHandler` already computes both figures this needs:

- `UpdateCampaignMapFirstSeenAsync` returns the number of rows it changed, which is exactly the count of tiles this Player is the first in the Game to see.
- `CountNewlyDiscoveredTilesByBiomeAsync`, summed across biomes, is the count of tiles new to this Player. Subtracting the first figure leaves the tiles somebody else saw first.

So the handler accrues `3 × firstEver + 1 × (newToPlayer − firstEver)` against `Exploration`, after both updates complete, inside one transaction that makes the accrual and the first-seen stamps atomic. The per-biome summary is reduced to a total, and the biome dimension is the movement rule's job instead.

**A refused accrual does not refuse the map read.** The experience catalogue is written by the database seed tool rather than by a migration, so a database the tool has never run against has no `ExperienceType` rows at all; refusing there would cost the Player their whole map to protect points that cannot exist. An accrual the allocator refuses is logged and the read continues. An accrual that *throws* still aborts the transaction and takes the stamps with it, which is the case where points really would be lost.

**The stamp is its own idempotency token.** A second read in the same Turn finds nothing newly discovered, because the first read set `FirstSeenOnTurnId`, so no guard is needed. Two concurrent reads for the same Player could both count the same tiles, since the count is read before the update — the same exposure the stamp itself already has, and not made worse here.

### Movement and settling, at end of Turn

Both are derived by `ExperienceAccrualReader`, called from `ApplyResearch` under its existing once-per-Round guard, and accrued through the same allocator the map read uses. **Deliberately not a new pipeline step:** a separate step would need a once-per-Round guard of its own, and the only place to keep one is a new column on `PlayerResearchState`, which would mean a migration for no gain.

Treading new ground reads this Turn's `MovementHistory` rows with `EventType.Moved`, takes distinct `CampaignMapTileId` values, groups by the tile's `CampaignMap.BiomeTypeId`, and drops tiles carrying a `StructureInstance` whose `RemovedOnTurnId` is null **and whose `BattleMapTileId` is null**. That second condition matters: a battle-map Structure still carries a campaign tile id, so without it a tile holding nothing but battle-map scenery would read as improved. The distinct is what makes the rule once per tile per Turn; `MovementHistory` is deliberately not deduplicated by tile, because the track layer needs the repeats.

Improvement is judged **when the derivation runs, not when the Force entered**. So a Force that crosses virgin ground and founds a camp there in the same Turn earns nothing for the crossing, and one that crosses a tile razed later in the same Turn keeps its point. Judging at entry would need the per-step producer state that approach C was rejected for.

Settling reads this Turn's `StructureHistory` rows with `EventType.Completed` whose Structure Instance has a `Settlement`, and groups by biome through `Settlement.StructureInstance.CampaignMap`. A founded-complete Structure — a camp that takes no work — still gets a `Completed` row, so it is covered. A Structure created outside any Turn, which is how Game setup places starting Structures, gets no history rows at all and so pays nothing.

Both run after `BuildingStructure` and `EvaluateForceOnHostileTerrain`, which is what the pipeline order already gives: completions this Turn are visible, and `MovementHistory` survives the attrition delete because it holds no foreign key to the Force.

### The distributor

`ApplyResearch` drains `PendingExperienceAccrual` for the Player, before it spends directed points:

1. Reads every accrual row for the Player and takes **one snapshot** of the recipient set: the Player's `PlayerTechnology` rows that consume each type through `TechnologyExperienceType`, whose status is Available or Unlocked, and whose experience requirement is non-null and not yet met. A null requirement is a bucket the technology does not use, and is not a recipient.
2. Splits each type's total evenly across that type's recipients, **accumulating the intended allocation per technology across every type before clamping anything**. A technology fed by two types is clamped once, against the headroom it had before the drain began, so the outcome cannot depend on which type is processed first.
3. Sends what is left to `PlayerUnusedExperience`: the remainder of an uneven split, the surplus from a clamp, and the whole total where no technology qualified. A Turn earning fewer points than there are recipients therefore loses all of them, which is the even split behaving as [technology.md](technology.md) specifies.
4. **Deletes** the Player's accrual rows, which is the contract `PendingExperienceAccrual` carries: the table is empty between Turns. Decrementing by the amount read would only differ for an accrual written by a concurrent map read, and would not save it either — the table has no concurrency token, so the `UPDATE` a decrement issues clobbers a concurrent write exactly as the `DELETE` does.

A technology whose experience bucket is filled here is **discovered here**, by the pass that already owns that decision for directed points: status, `DiscoveredOnRound`, `DiscoveryMethod` and all three reconcilers — availability, sustenance modifiers and extraction efficiency — run exactly as they do for a directed discovery. Skipping the reconcilers would discover a technology and apply none of its grants, which is the invisible failure the anti-goals rule out.

**A discovery here does not cascade within the Round.** Dependents that the availability reconcile promotes from Locked to Available become recipients from the next Round, because the split is taken against one settled snapshot.

The Experience row of `ResearchHistory` records the drained total as `Generated`, what landed in a bucket as `Spent`, and what went to `PlayerUnusedExperience` as `Lost`. It is written by `ApplyResearch` beside the Directed row, every Round, even for a Round that earned nothing: a Round of no progress reads as a zero rather than as a gap in the books.

### The recipient status, and the opening-Round windfall

The passive buckets fill from **Available**; only directed research requires `Unlocked`. That is [technology.md](technology.md)'s rule and this design keeps it, so a recipient is a row that is neither Locked nor Discovered. Acquisition cost gates the deliberate line of study, not what a Player picks up by doing things.

**The opening Round is generous under that rule, knowingly so.** Only four technologies consume `Exploration`, and none of them has a directed or diffusion requirement at all:

| Technology | Experience required |
| --- | --- |
| Fire-Making | 2 |
| Primitive Crafting 1 | 3 |
| Natural Exploration | 5 |
| Hunting | 5 |

All four are among the six technologies [technology.md](technology.md) lists as available at the start of a game, so fifteen points discovers every one of them. A starting field of view of 37 unseen tiles pays 111, which means all four are discovered on Round 1 — Primitive Crafting 1 among them, whose extraction-efficiency grants lift every material from the 5% harvest starting point to 100% — and the remaining 96 points go to `PlayerUnusedExperience`.

**That is a pricing problem, not a predicate problem.** The requirement figures are placeholder stand-ins, which [technology.md](technology.md) already carries as an open question, and no acquisition costs are configured anywhere in `TechnologySeedData`. Narrowing the predicate to `Unlocked` would hide it rather than fix it, and only by accident: nothing promotes a free technology to `Unlocked` except a Player selecting it as their project, so the recipient set would collapse to that one technology and experience would stop being passive in anything but name.

### Related, and not in this slice

Two changes outside this design would each improve it, and neither is a dependency:

- **Promoting free technologies to `Unlocked` eagerly.** `SetResearchProjectHandler.UnlockIfFreeAsync` is the only thing that settles a free technology's zero cost, and it fires only when a Player selects that technology as their project. Moving the promotion into `PlayerTechnologyAvailabilityService` would cover game setup, every discovery, start of turn and any catalogue seed at once, because that service already runs at all four. It must promote only rows that are **Available** — a technology whose prerequisites are not all discovered stays Locked, and never unlocks. This design does not need it: experience fills from Available either way.
- **Promoting `PlayerResearchState.NextTechnologyId` when a project completes**, which `ApplyResearch` already carries a `TODO` for. Without it, a project finished by experience makes `SpendOnProjectAsync` refuse on its `Status != Unlocked` guard, and the Round's whole directed pool is recorded as `Lost`. Experience makes that reachable, but the defect is the refusal, not the experience.

## Calls made here

These are settled, as the reading above rather than as decisions taken in conversation:

- **Any standing Structure makes a tile improved**, whoever owns it. A rival's watchtower stops the tile teaching you about its biome.
- **A Structure improves a tile from the moment it is founded**, at 0% complete, rather than from completion. It is occupied ground either way.
- **Entering is enough.** A Force that passes through a tile earns its point; it need not end its move there.
- **Every Settlement tier pays the same flat 10**, camp and village alike. Tier-scaled figures are a balance question, not a mechanism one.
- **Razing or capturing a Settlement claws nothing back.** Experience is what the Player learned, not what they hold.
- **Exploration is not per-biome.** Both sighting rules credit the single seeded `Exploration` type; the biome dimension is the movement rule's job.
- **`HidePlayerCampaignMapHandler` re-grants exploration experience, and is left alone.** It nulls the first-seen stamps, which are the only record that a tile was ever new, so the map read that follows pays the Player's whole exploration history over again. A tile whose global first-seen it cleared also pays the next Player to find it as though they were the first discoverer. It is a developer-only action that no production path reaches, and it is not defended against here.

## Where the code is

| Piece | Lives in |
| --- | --- |
| Biome and exploration codes | `Features/Technologies/Classes/ExperienceTypeCodes.cs` |
| The allocator every producer accrues through | `Features/Technologies/Services/ExperienceAllocator.cs` |
| The even-split arithmetic | `.../TurnPipeline/Campaign/EndTurn/ExperienceDistribution.cs` |
| The movement and settling derivations | `.../TurnPipeline/Campaign/EndTurn/ExperienceAccrualReader.cs` |
| The drain, the discoveries and both history rows | `.../TurnPipeline/Campaign/EndTurn/ApplyResearch.cs` |
| The exploration grants | `Features/Maps/CampaignMaps/Handlers/GetPlayerCampaignMapHandler.cs` |
