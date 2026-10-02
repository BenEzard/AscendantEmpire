# Intelligence and Sightings

What a player learns about the world **while they are not looking at it**, and how that reaches them
when their turn opens.

> This document is the design. For how *current* vision is computed and drawn — explored ground
> versus ground currently watched — see [Fog of War](../fog-of-war.md). For the sight and detection
> rules themselves, see [Vision on the campaign map](campaign-map/vision.md).
>
> **Status: partly built.** The `turns.PlayerTurn` foundation landed in F-21, `units.MovementHistory` with its single writer in F-22, the witness service with its coalition seam in F-23, the Forces producer in F-24, the Structures producer in F-25, and the combat producer in F-26: `battle.BattleHistory` with its own single writer, combat reports, and the fold wired into the campaign end-of-turn step. All three producers now exist. The fidelity curve (F-31) is not built.
>
> Two gaps are worth naming, because both leave a producer with no production input:
>
> - **Nothing in play writes a structure event.** Founding, construction, capture and razing are all still to be built, and each must go through `IStructureLifecycleWriter` when it is.
> - **Nothing in play writes a battle event, because there is no combat subsystem at all.** Nothing starts a battle, reinforces one, resolves one, damages a Unit or destroys a Force. Each must go through `IBattleLifecycleWriter` when it is built. The combat fold's **second host, end of battle turn, is not built either** - there is no battle turn pipeline and battle lanes open no Turns; the fold is a service precisely so that host can call it unchanged.
>
> Existing entities are called out where the design leans on them.

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
    structure.StructureHistory     founded / progressed / completed / captured / razed
    battle.BattleHistory           commenced / joined / left / concluded
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

One row per position change: the Unit, the tile entered, the turn, and the sequence. **Landed in F-22.**

Each row is one of three events — `Created`, `Moved` or `Destroyed` — so a Force's whole life during a Turn sits in one ordered feed. It also carries a copy of the Force's owner and template, so the row still says what the Force was after the Force has gone.

A `Moved` row also carries the tile the Force stepped **off**, and a check constraint makes that true of `Moved` rows and no others. The fold therefore only ever reads the current Turn's rows: without it, the first step a Force takes in a Turn would need that Force's last row from some earlier Turn, which may be many Turns back, or may not exist at all.

Every row belongs to a Turn. A Force placed **outside** any Turn gets no row — the starting armies at game setup, or a developer spawn into a Game not yet started. Nobody could have watched it appear, and there is no Turn to stamp it with. Its first move still says where it started.

`UnitInstanceId` is deliberately **not** a foreign key. A destroyed Force is hard-deleted, and the fold runs after attrition, so a Force watched and then killed in the same Turn reaches the fold with its `UnitInstances` row already gone. A foreign key would either block that delete or clear the column, and a cleared column would leave the fold unable to tell one dead Force's path from another's. Unit ids are never reused while any of these rows survive: the identity counter is only reseeded once the last Game has been deleted.

Rows are written only through `IForceLifecycleWriter`, and `UnitInstance.CampaignMapTileId` is init-only, so code that moves a Force any other way does not compile.

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

Everything else is a producer that asks it. It is the only place the answer is computed.

**Landed in F-23** as `IWitnessService`. It takes a batch of probes — a tile, a sequence, and optionally the Force the probe is about — for one Turn, and answers each with the coalitions that saw it and every one of their witnesses, nearest first, each with a distance and a fidelity. It has no production caller until the fold exists.

### It must be inverted

The naive implementation computes every other player's full field of view and tests the path against
it. That is one whole-map vision pass per opposing player per turn, to answer a question about a
handful of tiles.

Invert it. A Force's path is at most a few tiles long. For each tile on it, box-query opposing
observers within the maximum sight radius, then run the existing sight rules over that small set. The
cost then scales with path length and local observer density rather than with map size and player
count.

As built, the maximum sight radius is worked out per query: the best base Sight among the observing Players' Units, standing Structures and Units destroyed that Turn, plus the highest elevation bonus on any Geography a Force can enter. Mountains are left out — nothing stands on one to take the bonus — so the Void's elevation of ten cannot balloon every box. Each candidate Observer's field of view is then computed once and reused for every probe it covers.

### It requires an extraction first

The sight rules currently live inside `GetPlayerCampaignMapHandler`, entangled with the database
read, the response projection, and the `FirstSeenOnTurnId` side effect that stamps exploration as a
consequence of looking. `ObserverSightRules` is the beginning of pulling that apart and shows the
pattern: pure rules, no database, testable without a container.

The turn pipeline needs to ask "can B see this tile" without the read, without the projection, and
emphatically without the side effect. That extraction is a prerequisite, not a refactor to be done
afterwards.

**Landed in F-23.** The sight rules now live in one pure, database-free class, `FieldOfView`, over flat `SightObserver` and `SightTerrain` snapshots; `SightRadius` builds the sight tree it walks. Every query they depend on is in `ISightDataReader`, and `IFieldOfViewService` puts the two together. The live map read and `IWitnessService` both go through that service, so live vision and intelligence cannot drift apart.

The exploration stamp was deliberately **not** moved. `GetPlayerCampaignMapHandler` still stamps `FirstSeenOnTurnId` as a side effect of the read; the services it now calls have no side effects at all. Moving the stamp onto the movement path changes when ground becomes explored, and is its own piece of work.

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

As built, a Unit destroyed during the Turn has no `UnitInstances` row left, so it is rebuilt from its `MovementHistory` `Destroyed` row — owner, template, tile — with that row's sequence as its `DestroyedAtSequence`. It counts for sequences strictly before that one.

**F-25 does the same for Structures, and counts a capture as a loss.** A Structure razed during the Turn no longer stands, and one captured during it belongs to the acting Player, who is never an observing Player — so either way its owner's tower would stop witnessing everything it saw beforehand. `SightDataReader` therefore rebuilds any Structure with a `Captured` or `Razed` row this Turn as an Observer of **the Player who held it before that first loss**, with that row's sequence as its `DestroyedAtSequence`, and counts its Sight towards the search box. Only the first loss matters: every later owner within the Turn is the acting Player.

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

As built in F-24, the reference is a plain column rather than a foreign key, on both `PlayerReport.SubjectUnitInstanceId` and `PlayerKnownForce.UnitInstanceId`. The reason is the same as for `MovementHistory.UnitInstanceId`: a destroyed Force is hard-deleted, so a NoAction foreign key would block the delete and a SetNull one would erase which Force the row is about. The report column is nullable only because structure and combat reports will have no Force.

---

## Tracks

A moving Force does not generate one report per tile. It generates one report per **track**: a
maximal contiguous run of sequences during which it was visible to the coalition.

For each coalition, take the subsequence of the Force's steps that the witness service says they
could see, then split it wherever the sequence is not contiguous. Each resulting run carries:

> As built in F-24, "contiguous" means consecutive steps **of that Force**, not consecutive `TurnSequence` values. Every Force in a Turn draws from the one counter, so a Force watched the whole way still has gaps in its own sequence numbers, and splitting on them would cut every track apart.
>
> When a Force's first row in the Turn is a move, the tile it stepped off becomes an extra **origin step** at sequence 0. Without it, a Force parked in a Player's view since last Round that leaves this Turn would never be reported, and that Player could never be told which way it went.

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

As built, the cap is three and keeps the first two tracks and the last one, so the Force's final exit or still-visible state always survives. The known-Force row is refreshed from that last track, so the cap never makes a Player's memory stale.

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

As built in F-23, seeing a **tile** — its terrain and the Structures on it — is always *Detail*. Seeing a **Force** goes through `SightingFidelityRules.ForForce`, which is a deliberate stub returning *Detail*, the same as the live map already shows of any Force in sight. Its inputs are already carried in full: both sides' troops, the Force's template, the Biome and Geography it stands on, the Observer's Detection and distance, and the Turn, sequence, Observer and subject a deterministic random component would be seeded from. A Structure has no Detection stat yet and watches with a fixed `STRUCTURE_DETECTION`.

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

As built in F-24, that layer is `SightingDisclosure`, and both reads go through it. Movement discloses where and when: the path, heading, still-visible, appeared and destroyed. Type adds the template, Owner adds the owner and the Force's id, and Detail adds its strength. A fidelity that is not a defined level discloses nothing about the subject. A known Force's row id is never sent, because a stable id would link sightings of one Force whatever fidelity they earned. Two questions are left to the curve task, F-31, since neither can arise while every sighting is Detail: templates are player-owned, so a template at Type can give the owner away; and it is not yet decided how a low-fidelity sighting should update a known Force that was identified earlier.

---

## Reports

`game.PlayerReport` is the feed: one row per thing a player learned.

**One table, two kinds of subject, since F-25.** A report names a Force or a Structure, never both — a check constraint says so — and the Structure columns are nullable, so they cost a Force report nothing. A structure report reuses the record rather than extending it: its first and last tile are the Structure's one tile, it has no path tiles and no exit direction, `AppearedInView` means *founded in view* and `DestroyedInView` *razed in view*, and `CompletedInView` and `CapturedInView` carry the two events a Force has no equivalent of.

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

As built, the window is the reports raised during Turns after the reader's most recent **ended** Campaign Turn, compared by Turn id. No report is ever raised for a Player during their own Turn, so this gives the same reports as "since my last turn began". It is measured from the ended Turn because, while a Player is taking their Turn, their most recent Turn is the open one, and a window starting there would hide everything raised since their previous Turn. A Player who has never finished a Turn sees every report raised for them.

### Delivery

Reports are raised at the end of the *acting* player's turn, so they exist as soon as they are true —
a player planning moves mid-round sees fresh intelligence without waiting for their turn to open.

They are *surfaced* when the recipient's turn opens, through the `AlertSentries` slot in the
start-of-turn pipeline, which exists for exactly this and is currently a stub.
`StartTurnNotificationsOrder.SentryAlerts` and `InformationCategory` are the matching scaffolding.

**Changed in F-24.** Reports are raised by the `RaiseSightingReports` end-of-turn process, and surfaced by a read instead of by `AlertSentries`: `GET /intelligence/reports` for the feed and `GET /intelligence/known-forces` for the map layer, both taking the game and player from the game token. A start-of-turn process can only return an error string, so it has no way to show the player anything, and "reports since my last Turn" needs no work at turn start. `AlertSentries` stays a stub, for its own open question below: whether Forces on a sentry stance get a bonus.

On the client, the feed appears as a "Sightings" group in the notifications panel, newest first; clicking a report pans to where the Force was last seen. The sentence is built only from the subject fields the server disclosed, so a glimpse reads "Something seen at (4, 1)". Owners are named through `GET /player/game-players`, looked up when the feed is read. Known Forces are drawn only on tiles the player cannot currently see. The marker fades from fresh to a floor over five Rounds and then stays at the floor, since whether markers should ever retire is still open. Dismissing a report is remembered only in the browser, because the feed has no read state.

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

**`game.PlayerKnownForce` landed in F-24.** There is one row per Player per Force, upserted by the sightings step from the last track that Player's coalition saw. A sighting replaces the row only when it is at least as recent, compared on Turn id and then sequence, so an older Turn never rolls a memory back, and re-running the same Turn replaces the row with itself.

**`game.PlayerKnownStructure` was rebuilt in F-25** and now carries what its sibling does: the kind, the owner as last observed, the observed completion, a status of Standing or Razed, the fidelity, and the Turn and sequence it was last seen at. The recency rule is literally the same one, shared as `SightingRecency`. It is refreshed by every Structure sighting, including one that saw nothing but construction progress — that raises no report, but still moves the marker's progress on.

It only ever learns from **witnessed events**, though. A Structure a Player has looked at live for Rounds but never watched change has no row, so it still vanishes from explored ground the moment they lose sight of it. Remembering what live sight showed belongs with the exploration stamp, F-28.

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

**The seam landed in F-23** as `ICoalitionResolver`, implemented by `SoloCoalitionResolver`: every active Player is a coalition of one. The witness service widens an excluded Player to their whole coalition, so an ally sharing the acting Player's vision is never told what they watched on the same screen.

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

**Landed in F-25.** `structure.StructureHistory` records `Founded`, `Progressed`, `Completed`, `Captured` and `Razed`, each stamped with the Turn and a sequence from its counter, and written only through `IStructureLifecycleWriter` — the Structure sibling of `IForceLifecycleWriter`, keeping the same "no record, no change" rule. There is **no `Damaged` event**: a Structure has no health to lose until it gains a column for it. Razing is a soft delete, which is what lets the history and known-structure rows keep real foreign keys to the Structure.

Instead of tracks, the fold gathers **everything one coalition saw happen to one Structure in one Turn into a single sighting**, so founding a village and razing it in view is one entry in the feed rather than two. A Structure still standing is probed once more as at the end of the Turn, which is what `StillVisible` answers.

Two rules worth keeping in mind:

- **Construction progress raises no report.** Only a founding, a completion, a capture or a razing does; a witnessed `Progressed` step refreshes the known Structure and nothing more. A Structure founded *and* finished within one Turn reads only as "completed construction" — the start is not news once the finish is in the same sentence.
- **Only the acting Player is excluded from witnessing.** The Force fold also excludes the Force's owner, because only the acting Player's Forces move during their Turn. A Structure event happens to *somebody's* Structure, and that somebody watching their own tower taken or razed is intelligence they legitimately hold.

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
removing it. That is the **Force** producer's job, not this one's: it follows from the `Destroyed` row
`IForceLifecycleWriter` writes, and works the moment combat calls it. Nothing calls it yet.

**Landed in F-26.** `battle.BattleHistory` records `Commenced`, `ParticipantJoined`, `ParticipantLeft`
and `Concluded`, each stamped with the Turn and a sequence from its counter, and written only through
`IBattleLifecycleWriter` - the Battle sibling of the other two writers, keeping the same "no record, no
change" rule. Four rules are worth keeping in mind.

- **Losses sit per side, on that side's own `ParticipantLeft` row**, never as a battle-wide total on the
  conclusion. The fold then sums only the departures a coalition actually watched, so a watcher who saw
  one side break and then lost sight of the field is told that side's losses and no more. A column pair
  on the conclusion was rejected outright: a tile can host a battle with more than two sides, and a pair
  would silently produce a false sentence for a three-way fight. It is also what earns the four-event
  feed its keep - concluding writes a `ParticipantLeft` row per still-in side *and then* the `Concluded`
  row, so "the battle ended" and "this is what each side lost" are separate facts on separate sequences
  with separate witness answers.
- **Ground held is snapshotted** on the `Concluded` row rather than read from
  `CampaignMap.ControlledByPlayerId` at fold time. That column is the position *now*, and the fold is
  re-runnable, so a re-run after the tile changed hands again would answer the same question differently
  - the same reason `StructureHistory.CompletionPercent` is snapshotted. It is deliberately not the
  victor either: a victor may withdraw, and what a watcher on the next hill sees is whose banners are on
  the ground afterwards.
- **Participants are excluded from witnessing their own battle,** alongside the acting Player. This is
  the opposite of the Structure rule, and for a reason: a Structure is an object *acted upon*, so its
  owner watching it taken is real intelligence, while a combatant is an *actor* who is already in the
  fight with a roster place and a battle lane of their own. Crucially a participant need not be the
  acting Player - a battle between A and C during A's Turn leaves C non-acting - so the acting-Player
  exclusion does not already cover it. Because the excluded set differs from battle to battle, the
  combat fold issues **one witness query per engagement**, the way the Force fold issues one per owner.
  A single query excluding every combatant would silence a side in one battle from witnessing another
  halfway across the map.
- **An engagement still being fought gets the end-of-turn probe and a concluded one does not,** exactly
  as a razed Structure does not, so `StillVisible` on a combat report means the fighting was still going
  on and still in view when the Turn ended.

**No known-object row.** There is no `PlayerKnownBattle` and no map marker: an engagement is a
point-in-time event, not a thing standing somewhere whose position a Player goes on remembering. What a
watcher is left holding is the report.

**The roster is deliberately not disclosed.** "Red Empire and Blue Empire were seen fighting" is not on
the campaign-map-visible list above, and a flat report row cannot hold an N-sided roster without a child
table. Adding one is a follow-up, not an omission.

---

## Where it runs

| Phase | What happens |
| --- | --- |
| During a player's turn | Producers append raw rows, each stamped with the turn and a sequence from the turn's counter. |
| End of campaign turn, last | The fold: witness service over the raw rows, then reports and known-object rows written. Placed after attrition, so a Force destroyed by hostile terrain is still reported as seen. As built, this is `RaiseSightingReports` over `IForceSightingFoldService`, then `IStructureSightingFoldService`, then `ICombatSightingFoldService`. If any of them fails, the Turn does not end, because ending it would lose those sightings for good. All three are idempotent (a re-run replaces the Turn's reports of that type in one transaction), so retrying is safe. |
| End of battle turn | The same fold, for the combat producer only. **Not built.** There is no battle turn pipeline - the four `I*BattleProcess` marker interfaces are empty, with no ordering enum and no handler - and battle lanes have no `PlayerTurn` rows. `ICombatSightingFoldService` is keyed on a Turn rather than on a Game so that host can call it unchanged when it exists. |
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
- Whether assets and infrastructure discovered by an observer belong in this feed too, or stay
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
2. ~~**A single writer for Force position and lifecycle.**~~ **Landed in F-22.** `IForceLifecycleWriter` is the only way a Force is created, moved or destroyed, and each change is written together with its `units.MovementHistory` row: if the Turn's sequence cannot be allocated, the change is not made. The step handler, the developer spawn and game setup all go through it. Pathfound order execution, retreats and settlement founding do not exist yet, and nothing destroys a Force yet; each of them must go through the writer when it is built.

   **F-25 added the Structure half,** `IStructureLifecycleWriter`, on the same terms. Nothing calls it yet either: settlement founding, the `BuildingStructure` construction step, and siege must each go through it when they are built.

   **F-26 added the Battle half,** `IBattleLifecycleWriter`, again on the same terms. Nothing calls it yet: starting a battle, reinforcement, retreat and resolution must each go through it when combat is built - including `CreateBattleTurnQueueHandler`, which builds a `Battle` and its roster directly today. That handler has no production caller and no route, so it is harmless as it stands, but it is a live bypass of the writer and must not gain one.
3. ~~**The witness service extraction** described above.~~ **Landed in F-23.** `FieldOfView` holds the sight rules, shared by the live map read and `IWitnessService`; see [The witness service](#the-witness-service).
