# Intelligence and Sightings

What a player learns about the world **while they are not looking at it**, and how that reaches them
when their turn opens.

> This document is the design. For how *current* vision is computed and drawn — explored ground
> versus ground currently watched — see [Fog of War](../fog-of-war.md). For the sight and detection
> rules themselves, see [Vision on the campaign map](campaign-map.md#vision-on-the-campaign-map).
>
> **Status: proposed.** The `turns.PlayerTurn` foundation landed in F-21; nothing else described here is
> built. Existing entities are called out where the design leans on them.

---

## The gap this closes

Ascendant Empire is played asynchronously: players take their campaign turns one after another, and
are rarely logged in at the same time (see [turn-structure.md](turn-structure.md)). Fog of war is
computed *live*, from where a player's observers are standing at the moment of the read. Those two
facts combine into a hole.

Player A moves a Unit into Player B's field of view and back out again, all within A's own turn. When
B next logs in there is no observer anywhere near that ground, so the read reports nothing, and B has
no way of ever knowing A was there. The same hole swallows:

- a Unit that moved **through** B's field of view, in either direction;
- a Unit that **entered** B's view and stayed — B sees it on the map, but cannot tell it is new;
- a Structure founded and razed within one turn;
- a Structure founded in view — again visible, but with nothing to say it wasn't always there;
- a battle fought between A and C on ground B is watching.

Live vision answers "what can I see *now*". It cannot answer "what happened here while I was away",
and in an asynchronous game that second question is most of the game.

## The principle

> Sight is a state. Intelligence is a record of the past.

Everything in this document follows from keeping those two apart, exactly as
[Fog of War](../fog-of-war.md) keeps *explored* apart from *watched*. Live vision stays live and
stateless. Intelligence is written down when it is witnessed, and is never taken back.

---

## Vocabulary

| Term | Meaning |
| --- | --- |
| **Witness** | An observer — Unit, Command or Structure — that could see a given tile at a given moment. |
| **Sighting** | The fact that a witness saw something. |
| **Track** | One contiguous run of steps during which a moving Force was continuously visible to a coalition. A Force crossing a field of view twice in one turn produces two tracks. |
| **Fidelity** | How much a witness learned. Ranges from "something passed" to full identification. |
| **Coalition** | The set of players who share vision. Today every player is a coalition of one; see [Coalitions](#coalitions). |
| **Report** | A single entry in a player's intelligence feed. |
| **Known force / known structure** | A player's persistent memory of an object's last observed position and state. |

---

## The shape

```
Raw feeds — written as the acting player acts, each row stamped (PlayerTurn, TurnSequence)
    units.MovementHistory          position changes
    structures.StructureHistory    founded / progressed / damaged / razed / captured
    battles.* engagement records   combat outcomes visible from the campaign map
          │
          ▼
    Witness service ── "which coalitions could see tile T at sequence N, and at what fidelity?"
          │
          ├──► game.PlayerReport           the feed, read at turn start
          ├──► game.PlayerKnownForce       last-known intel, drawn on the map
          └──► game.PlayerKnownStructure   ditto, for structures
```

Three producers, one witness service, one report table. The producers know what happened; the witness
service knows who saw it; the reports and known-object tables are what the player is left holding.

The centre of gravity is deliberately the **witness service**, not the movement log. Movement history
is not the feature — it is the first of three feeds that ask the same question.

---

## Raw feeds

Each acting player's turn writes an append-only record of what they did. These are facts about the
world, not about anybody's perception of it, and they are written **whether or not anybody was
watching** — the witness question is asked later.

### Why three tables rather than one

A single generic activity table with a discriminator and a JSON payload was considered and rejected.
Movement history is high-volume, narrow and hot — a Force, a tile, a sequence number — and wants a
tight index and real foreign keys. Structure lifecycle and combat records are low-volume and carry
quite different columns. Forcing all three into one shape costs the foreign keys and buys nothing,
because the thing they genuinely share is the *question they ask*, not the columns they hold.

### One ordering across all three

They do, however, need a single interleaving order: "rode past the village, then razed it" and "razed
the village, then rode past" are different intelligence, and they can only be told apart if the
movement rows and the structure rows sort against each other.

`turns.PlayerTurn` therefore carries a monotonic counter, and every raw row in every feed draws its
`TurnSequence` from it. That gives a total order across the turn without forcing the feeds into one
table.

### `units.MovementHistory`

One row per position change: the Unit, the tile entered, the turn, and the sequence.

Deliberately **not** carrying a health or strength snapshot. Health is redundant with `UnitInstance`,
it bloats the highest-volume table in the design, and it is the wrong layer: what matters is what an
*observer perceived*, which belongs on the sighting and only at the fidelity that earned it.

Positions are **not** deduplicated by tile as they are written. Deduplication destroys the ordering
that makes an exit direction computable, and a Force may legitimately re-enter a tile it has already
left. Collapsing happens at the track layer, where it is a presentation concern.

---

## The witness service

One service, one question:

> Which coalitions could see tile `T` at sequence `N`, and at what fidelity?

Everything else is a producer that asks it. It is the only place the answer is computed, in the same
way that `GetPlayerCampaignMapHandler` is today the only place fog of war is computed.

### It must be inverted

The naive implementation computes every other player's full field of view and tests the path against
it. That is one whole-map vision pass per opposing player per turn, to answer a question about a
handful of tiles.

Invert it. A Force's path is at most a few tiles long. For each tile on it, box-query opposing
observers within the maximum sight radius, then run the existing sight rules over that small set. The
cost then scales with path length and local observer density rather than with map size and player
count.

### It requires an extraction first

The sight rules currently live inside `GetPlayerCampaignMapHandler`, entangled with the database
read, the response projection, and the `FirstSeenOnTurnId` side effect that stamps exploration as a
consequence of looking. `ObserverSightRules` is the beginning of pulling that apart and shows the
pattern: pure rules, no database, testable without a container.

The turn pipeline needs to ask "can B see this tile" without the read, without the projection, and
emphatically without the side effect. That extraction is a prerequisite, not a refactor to be done
afterwards.

### Observers destroyed mid-turn

During A's turn every other player's observers are frozen — nobody else is acting. There is one
exception, and it matters: **A can destroy them**.

If A's cavalry rides past B's watchtower at sequence 3 and razes it at sequence 9, a witness pass
that runs at end of turn finds no observer there and B learns nothing at all. The tower saw the
cavalry and then died, and the intelligence dies with it silently.

The design therefore evaluates witnesses **as at the sequence of the event**, not as at end of turn.
Observers destroyed during a turn are marked with the sequence at which they died, and count as
witnesses for every sequence before it. A tower that saw something and was then destroyed still files
its report.

> The alternative — "a dead observer files no report" — is simpler and is defensible as a game rule,
> since it rewards decapitation strikes. It is rejected because it makes intelligence silently
> dependent on the order in which the attacker chose to act, which is not a distinction the player
> can see or reason about.

### The sighted object may not survive either

`EvaluateForceOnHostileTerrain` runs at the end of the turn pipeline and can destroy the acting
player's own Units. So can combat. A Unit that was seen at sequence 3 may not exist by the time
reports are folded.

Sightings therefore hold a **nullable** reference to the Unit with no cascade delete, alongside a
denormalised snapshot of what was perceived. Intelligence about a Force that no longer exists is
still intelligence — and knowing you watched something die is among the most valuable kinds.

---

## Tracks

A moving Force does not generate one report per tile. It generates one report per **track**: a
maximal contiguous run of sequences during which it was visible to the coalition.

For each coalition, take the subsequence of the Force's steps that the witness service says they
could see, then split it wherever the sequence is not contiguous. Each resulting run carries:

| Field | Meaning |
| --- | --- |
| `firstTile` | Where the coalition picked it up. |
| `path[]` | The ordered tiles, consecutive duplicates collapsed. |
| `lastTile` | The last tile seen. |
| `exitDirection` | The direction of the step that took it out of view, or null if it never left. |
| `stillVisible` | Whether it was still in view when the turn ended. |

That one structure renders every case the feature needs:

| Track | Renders as |
| --- | --- |
| Single tile, not still visible | "seen at (X, Y), heading north-east" |
| Single tile, still visible | "seen at (X, Y)" |
| Multiple tiles | "seen passing through (X, Y), (X2, Y2)" |
| Multiple tiles, not still visible | "seen passing through (X, Y), (X2, Y2), heading north-east" |

### Fold per coalition, not per observer

If three of B's Units watched the same road, B gets **one** report, not three. Visibility is the union
across the coalition's observers, and tracks are derived from that union.

The first observer to pick the Force up is recorded on the report anyway, because "your sentry at
Redhill reports" is worth having, and because under [coalitions](#coalitions) the observer may belong
to somebody else.

### Exit direction is earned, not leaked

Reporting the direction a Force left in is legitimate: an observer genuinely watches it walk off the
edge of their vision, and the step that did so is a real observation. What the observer does **not**
learn is anything the Force did afterwards. A Force that leaves heading north-east and immediately
turns south is reported as heading north-east, and the report is not corrected. Intelligence is
allowed to be stale; it is not allowed to be omniscient.

### Track cap

Tracks per Force, per coalition, per turn are capped — three is a reasonable starting figure — so that
a scout circling a watchtower cannot flood the feed.

---

## Fidelity

A sighting is not all-or-nothing. `CompiledUnitTemplate.TotalDetection` and `TotalVisibility` are
already computed and stored, and are currently used by nothing; this is where they earn their keep.

| Level | The observer learns |
| --- | --- |
| **Movement** | Something passed through (X, Y). Nothing about what. |
| **Type** | What kind of thing it was — a settler, a scout. |
| **Owner** | Whose it was. |
| **Detail** | Strength and condition. |

Fidelity is a function of the observer's detection against the target's visibility, modified by
distance and by the terrain the target was standing in. The precise curve is an open question; the
*shape* is not.

### Fidelity is a projection rule, not a storage rule

This is the important consequence, and it is easy to get wrong.

Store the **truth**. A sighting always records the real `UnitInstanceId`, whatever fidelity the
observer achieved. The read projection then strips identity down to the fidelity that was earned.

The tempting alternative — writing anonymous "contact" rows at low fidelity — creates a
reconciliation problem the moment fidelity improves. A Force seen vaguely in round 3 and identified in
round 8 becomes two unrelated rows describing the same thing, and nothing can ever join them.

The cost of storing the truth is that fidelity becomes a **disclosure rule enforced in one projection
layer**. That layer is security-relevant: a leak there hands a player the identity of a Force they
only glimpsed. It gets its own tests, testing the redaction directly rather than testing it through a
handler.

---

## Reports

`game.PlayerReport` is the feed: one row per thing a player learned.

Reports are **typed and structured** — report type, subject, tile ids, direction, fidelity, round,
first witness — and carry **no pre-rendered text**. The sentence is composed in the frontend.

> A rendered sentence in a database column cannot be put on a map, cannot be filtered, cannot be
> translated, and cannot be re-labelled when the relations model lands and "Foreigner" becomes
> "Ally". The three sentence forms in the table above are not three report types; they are three
> renderings of one record.

For the same reason a report stores the **observed player id**, never a relationship word. Whether
that player is an enemy, an ally or a stranger is derived at read time from the relations model, so
that a report written before an alliance reads correctly after it.

### No read state

Reports carry no read/acknowledged flag. "New since I last looked" is derived from turn identity
instead: the feed shows reports raised **since the player's last turn began**.

That makes first-class `turns.PlayerTurn` load-bearing rather than merely tidy — it becomes the only
thing that can answer the question. It also means a player who reads their feed, leaves, and returns
mid-round sees the same list again, which is accepted.

### Delivery

Reports are raised at the end of the *acting* player's turn, so they exist as soon as they are true —
a player planning moves mid-round sees fresh intelligence without waiting for their turn to open.

They are *surfaced* when the recipient's turn opens, through the `AlertSentries` slot in the
start-of-turn pipeline, which exists for exactly this and is currently a stub.
`StartTurnNotificationsOrder.SentryAlerts` and `InformationCategory` are the matching scaffolding.

---

## Known forces and known structures

A report is read once. A **known force** persists.

`game.PlayerKnownForce` records a player's last observed position and state for a Force, and is the
sibling of the existing `game.PlayerKnownStructure` — which is written, wired into the context, and
so far used by nothing. Both are refreshed by the same fold that raises reports.

This is what makes the feature feel like intelligence rather than a stream of notifications: a
last-known-position marker on the map, ageing visibly, is a thing a player can plan against.

| Rule | Why |
| --- | --- |
| Never deleted, only superseded or restatused. | You cannot unknow a position. |
| Carries a status — sighted, destroyed, and so on. | A Force you watched die is knowledge, not an absence of it. |
| Carries the round it was last seen. | The client ages the marker visually; there is no server-side expiry. |
| Never revoked when an alliance breaks. | Same reason as the first rule. Intelligence obtained legitimately stays obtained. |
| Subject to the same fidelity projection as reports. | Otherwise the map layer becomes the leak the report layer prevents. |

`PlayerKnownStructure.ObservedCompletionPercent` is already the right shape for a fidelity-gated
snapshot, and stops being dead weight the moment the structure producer exists.

---

## Coalitions

Vision may be shared between allies. The fold therefore groups by **coalition**, not by player, while
reports are still addressed to individual players.

Consequences:

- A report names **whose** observer saw it, and carries **that observer's** fidelity — not the
  reader's. An ally with a good scout sees more than you would have.
- One coalition-level track fans out into one report per member player.
- Reports and known-forces already written are unaffected by a later change of relationship. They are
  historical facts.

There is no foreign relations model in the codebase yet — [foreign-relations.md](foreign-relations.md)
is design only. So this design calls for the **seam, not the model**: a coalition resolver that today
returns each player as a coalition of one, and later consults foreign relations. Building the seam now
makes shared vision a wiring exercise rather than a rework of the fold.

---

## What each producer emits

### Forces

Founded on `units.MovementHistory` and tracks, as above. Also emits on Force creation and destruction
within view, since both are events a witness genuinely sees.

### Structures

A structure does not move, so there are no tracks — a structure event is a point fact at a single
tile and sequence: founded, progressed, damaged, razed, captured.

Both the transient and the persistent cases matter. Transient is the obvious hole: A founds a village
at sequence 3 and razes it at sequence 9, and B, watching, currently learns nothing. But the
persistent case is just as real: A founds a village in B's view and it is still standing at turn end,
so B *can* see it — with nothing whatever to indicate it is new. On a large map that is a change the
player will simply miss.

### Combat

A battle fought between A and C on ground B watches is visible to B, and today reports nothing.

Two wrinkles:

1. **Battles run on their own rosters**, and may resolve outside any campaign turn
   (see [turn-structure.md](turn-structure.md)). The fold therefore has two hosts: end of campaign
   turn, and end of battle turn. This is a further reason for it to be a service the pipeline calls
   rather than a pipeline process that owns the logic.
2. **Only what is visible from the campaign map is reported** — that an engagement began at (X, Y),
   that it ended, who held the ground afterwards, and at *Detail* fidelity a rough sense of losses.
   Not the battle-map blow-by-blow, which a distant observer has no way of perceiving.

A Force destroyed in view updates the observer's known-force record to *destroyed* rather than
removing it.

---

## Where it runs

| Phase | What happens |
| --- | --- |
| During a player's turn | Producers append raw rows, each stamped with the turn and a sequence from the turn's counter. |
| End of campaign turn, last | The fold: witness service over the raw rows, then reports and known-object rows written. Placed after attrition, so a Force destroyed by hostile terrain is still reported as seen. |
| End of battle turn | The same fold, for the combat producer only. |
| Start of the recipient's turn | `AlertSentries` surfaces the feed. |

---

## Deliberate exclusions

- **The acting player gets no report of what *they* saw.** Their client showed it to them live. A
  symmetric log is defensible but is not this feature.
- **A player is never told they were seen.** A observing that B noticed them would be intelligence B
  never transmitted.
- **Raw feed retention is not addressed.** Eight players over two hundred rounds with thirty Units
  each averaging four steps is under two hundred thousand movement rows, which is nothing. The feeds
  can be left indefinitely, and only revisited if they are kept for replay.

---

## Open questions

- The exact fidelity curve — how detection, visibility, distance and terrain combine into a level.
- Whether a Force on a sentry stance gets a fidelity or range bonus, which is the obvious reason for
  `AlertSentries` to be named what it is.
- Whether resources and infrastructure discovered by an observer belong in this feed too, or stay
  part of the map read.
- Whether known-force markers should ever be automatically retired, or only ever fade in the client.
- What an AI player does with a report feed.

---

## Dependencies

This design rests on three things that do not exist yet, and each is a prerequisite rather than a
parallel workstream:

1. ~~**First-class `turns.PlayerTurn`,** carrying the sequence counter.~~ **Landed in F-21.** The table
   exists, is opened before the start-of-turn pipeline and closed after the end-of-turn pipeline, and
   carries `LastAllocatedSequence`. `IPlayerTurnService.AllocateSequenceAsync` hands out the
   `TurnSequence` values every raw feed row is to be stamped with, atomically. Nothing writes a feed row
   yet — that is dependency 2.
2. **A single writer for Force position and lifecycle.** Movement is written from more than one place
   today: immediate steps, pathfound order execution, retreats, spawns, and the context actions that
   found settlements. If history is appended at each call site, one will be missed — and a missing
   path fails *silently*, as intelligence that simply never arrives.
3. **The witness service extraction** described above.
