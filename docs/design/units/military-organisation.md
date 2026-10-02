# Military Organisation

## Overview — the model at a glance

Military organisation is built from a few concepts that are **separate but associated**.
The two that are easy to conflate — *rank* and *organisation* — are deliberately kept
apart and joined by a single positional rule:

> **A note on the word "formation".** Everywhere else in the design set it has been retired, because it
> meant three different things across documents (see [Terminology.md](../../Terminology.md)). It survives
> here, lower-case and with one narrow meaning: **a grouping of soldiers at one tier inside a Unit** — a
> company, a platoon, a battalion. That is deliberately neither a `Unit`, a `Command` nor a `Force`; it is
> the internal structure of a single Unit, and it needs a noun because "tier" names the level rather than
> the thing sitting at it.

* **OrganisationStructure** — the *shape* of a Unit: an ordered list of 1–5 tiers,
  each with an authored size. Reusable; owned by nothing in particular.
* **MilitaryOrganisation tag** — a *pointer* to one OrganisationStructure. A tag is just a
  named handle; the structure it points at is the real definition.
* **Leadership model** — authored *on the tag*: **Commissioned** (the ranked hierarchy this
  document mostly describes) or **Unled** (no officers at any tier — scouts, snipers, drones,
  settlers). Unled Units sit entirely outside the officer economy.
* **Rank (OfficerClass)** — the *seniority* of an individual officer. Supplies a **hard
  ceiling** on formation size and a cap on attached specialists. It never defines a
  formation's size — it only limits it.
* **Officer** — a person with a permanent movement domain and a rank, grown from combat.
* **Population cap** — how many officers of each rank a player may hold at once.

For **Commissioned** structures, the join between shape and seniority is **positional**:
*tier N is commanded by a Rank-N officer*. Because tiers ascend, chain-of-command seniority
is automatic — a commander always outranks the tier directly beneath him. **Unled**
structures never enter that join — see [Leadership model](#leadership-model).

---

## Core concepts

### OrganisationStructure

An ordered list of **1 to 5 tiers**, from smallest (tier 1) to largest (tier N). It is the
reusable definition of a formation's internal shape. Many templates and components can
share one structure by pointing at it through a tag.

Each **tier** carries:

* a **name** — customisable, with domain-flavoured defaults (Land: *Company → Platoon →
  Battalion → …*; Sea: *Ship → Squadron → Flotilla → …*).
* an **operative size** — the authored strength of a single formation at that tier
  (e.g. Infantry tier 1 = 10 men).
* an implied **rank requirement** — its tier index. Tier 1 → Rank 1, tier 5 → Rank 5.

**Tier sizes must be integer multiples of the tier below** — a tier always contains a whole
number of the formations beneath it (Infantry 50 = 5 × 10, 100 = 2 × 50, …). Non-integer
ratios are **not permitted**, so officer counts are always whole and formations always divide
cleanly.

### MilitaryOrganisation tag

A **pointer** to one OrganisationStructure — *not* an embedded copy. Editing the structure
updates every tag that points at it.

A tag reaches a Unit Template through three sources, in increasing specificity (later wins):

1. **Body default** — the Body component seeds a default tag (a human body → `Infantry`).
2. **Component override** — equipping a component can swap the tag
   (e.g. adding `camping-goods` applies the `Settler` tag).
3. **Manual override** — the player authors or selects a different tag *on the template*
   in the Unit Designer.

Players may keep many structures and tags, and different templates may use different ones.

Beyond being a pointer, the tag carries a small amount of **authored** (catalogue-level,
non-player-editable) data: its movement domain, its **leadership model**, and — for unled
tags — the unled size values (see below). The tag is the right home for authored guardrails
precisely *because* the player cannot edit it: per-player data (the structure's tiers) is
where player freedom lives, and the tag is where the limits on that freedom live.

### Leadership model

Not every Unit is a military hierarchy. A settler caravan, a lone scout, a drone — these
have no officers, and pretending otherwise (free officers, hidden officers) creates entities
that then need special-casing everywhere. Instead, each MilitaryOrganisation tag declares a
**leadership model**:

| | **Commissioned** | **Unled** |
| --- | --- | --- |
| Officers | tier 1 free and ascending; tiers 2+ earned from the roster | none, at any tier |
| Legal sizes | only the authored tier sizes (10 / 50 / 100 / …) | any `n × Base`, up to `Max` |
| Why | each level needs a commander of matching rank | nothing constrains the grouping |
| Size bound | rank ceiling, per tier | tag-authored `Max` |
| Splitting | detach a whole formation; its officer goes with it | peel off quanta (see [Splitting and merging](#splitting-and-merging)) |
| Officer economy | full participant | outside it entirely: no roster slots, no population-cap consumption, no Command Experience, no ascension |

An unled tag is **not** officer suppression — it is a declaration that the Unit is not a
commissioned hierarchy, so no officer entity is ever created and there is nothing to hide.

Two alternatives were considered and rejected:

* **Free officers.** Free ≠ absent: a free officer still exists, still accrues Command
  Experience and still ascends — so a cheap, risk-free unled blob becomes an *officer farm*
  feeding the ladder. It also hides entities rather than removing them.
* **A suppression ability tag on the template.** Organisation tags already point at
  structures; a second independent "no officers" axis can contradict the structure it is
  attached to — two sources of truth for one question.

The leadership model is **authored on the tag**, never on the player's structure — if it
lived on player-editable data, a player could toggle Infantry to Unled and walk out of the
entire officer economy.

`Command` is structurally unaffected: a Command's commander is external to its Units, so an
unled scout can still sit inside a Command, and the *(max tiers)+1* top rank is unchanged.
(Whether unled units *should* be allowed into Commands is an open balance question — see
[Future review](#future-review).)

### Rank (OfficerClass)

Rank is an attribute of an **individual officer** — their seniority — not of a formation.

* Ranks are **unlocked per player** (Rank 1 is always available).
* A rank's **title is `rank × movement domain`** — the same underlying Rank 6 reads
  "General" for Land and "Admiral" for Sea. Titles are player-editable, for free, at any time.
* Rank supplies two **limits**, held in the OfficerClass table:
  * **Max soldiers (hard ceiling)** — the most a tier commanded at this rank may ever be
    sized to. This is a *ceiling*, not a definition (see [Size model](#size-model--operative-vs-ceiling)).
  * **Max attached specialists** — how many specialists a formation led at this rank may hold.

**Ladder height = (max tiers) + 1.** With a maximum of 5 tiers, the rank ladder must be 6
tall: five ranks for the tiers of a maximally-deep Unit, plus one more for the single
commander that sits above it (the `Command` layer). This is exactly why the table has 6 rows.

#### OfficerClass table

| Rank | Land title | Sea title | Air title | Max soldiers (hard ceiling) | Max attached specialists |
| --- | --- | --- | --- | --- | --- |
| 1 | Lieutenant | Ensign | Pilot Officer | 44 | 1 |
| 2 | Captain | Lieutenant | Flight Lieutenant | 200 | 3 |
| 3 | Major | Commander | Squadron Leader | 1,000 | 5 |
| 4 | Colonel | Captain | Wing Commander | 3,200 | 10 |
| 5 | Major General | Commodore | Air Commodore | 10,000 | 12 |
| 6 | General | Admiral | Air Marshal | — (governed by max Units in a Command) | 15 |

Titles are **example defaults** — the player may rename any of them, for free, at any time.
Rank 6 has no soldier ceiling of its own; a `Command` at Rank 6 is bounded by its max number
of Units instead.

### Size model — operative vs ceiling

The numbers in play, deliberately stored in different places:

| Number | Applies to | Lives on | Player-editable? | Role |
| --- | --- | --- | --- | --- |
| **Operative size** | Commissioned | the structure's **tier** | yes | what this formation actually holds |
| **Hard ceiling** | Commissioned | the **rank** | no (fixed) | the most a tier at that rank may be set to |
| **Base (quantum)** | Unled | the **tag** | no (authored) | the indivisible group size; all sizes are multiples of it |
| **Default** | Unled | the **tag** | no (authored) | the size a unit takes when created without an explicit choice |
| **Max** | Unled | the **tag** | no (authored) | the largest a single unled unit may be |

For **Commissioned** structures the **constraint is enforced against the tier** but the
**ceiling value lives on the rank**:

```
Commissioned:  structure.tier[N].operativeSize  ≤  rank[N].maxSoldiers
Unled:         unit.size = n × tag.Base,  n ≥ 1,  tag.Base ≤ unit.size ≤ tag.Max
```

The player may adjust a Commissioned tier's operative size **within the range** up to its
rank ceiling. The ceiling must live on the rank (which the player cannot edit per-structure)
— if it lived on the tier, a player could simply raise it, and the guardrail would mean
nothing. The same argument places the unled values on the **tag**: the rank ceiling models
how many men one officer can effectively lead, so with no officer it measures nothing, and
the tag's authored `Max` replaces it as the guardrail. The player's freedom moves to
**creation time** instead: an unled unit may be created at any multiple of `Base` up to
`Max` (e.g. a settler party of 50, 500 or 5,000), with `Default` used when no size is chosen
(e.g. game-start spawns).

Unled tags therefore need **no per-player structure at all** — there are no tiers to author.
The Military Organisation page shows unled tags read-only.

* **Floor (Commissioned):** a tier must contain a whole number **≥ 2** of the formations
  beneath it — a fan-out of 1 would just be a pointless duplicate of the tier below. So the
  smallest legal size for tier N is **2 × (tier N-1)**, and any operative size is a multiple
  `m × size[N-1]` with `m ≥ 2` that still fits under the rank ceiling. Unled tags have no
  tiers, so the rule does not apply to them; their floor is a single quantum (`Base`).
* The old `UnitComponentBody.MinimumUnitSize` / `MaximumUnitSize` fields are **removed** —
  size bounds now come from the structure tier and rank ceiling (Commissioned) or the tag's
  authored `Base`/`Max` (Unled), never from the body component.

### Officer

An Officer is a person and a type of **embedded** `Specialist`.

* Every officer has a **permanent movement domain** (Land / Sea / Air) — set at creation and
  not transferable. A player's officer roster therefore partitions naturally by domain, and
  officers are assigned to formations of the matching domain.
* An officer holds a **rank**, which is grown from combat (see
  [The officer economy](#the-officer-economy)).
* The senior-most officer in a `Command` determines how many `Units` it may contain and how
  many `Specialists` may be attached to it. (A Unit itself has no command experience.)

---

## The officer economy

Officers are primarily **grown from combat**, not bought. Bodies are cheap; a seasoned
officer corps is the scarce, strategic asset.

### The bottom-tier officer is free

The tier-1 (`Company`) officer is provided **free** with the formation. Every deeper tier
requires a real, earned officer.

### Ascension and backfill

1. A company spawns with a **free Rank-1 officer**.
2. As it earns **Command Experience**, that officer eventually **ascends to Rank 2**.
3. On ascension the officer **vacates the company and moves up** to command the tier-2
   formation; the company **mints a fresh free Rank-1 officer** by promoting one man out of
   the ranks (that man is removed from the soldier count).

A higher tier may be **assembled from its sub-formations before a qualifying commander
exists** — it operates with a vacant (acting) top slot, and the experience it earns is
exactly what promotes one of its sub-officers up into that slot. That resolves the
chicken-and-egg: you never need an existing Rank-N officer to *start* growing one.

Net effect: growing a Rank-5 general means sustaining a real, adequately-manned 5-tier
Unit in action long enough for an officer to climb all five rungs, backfilling at the
bottom each time.

### Command Experience accrual gate

* Experience accrues **only while the officer is embedded in the next tier up**, and that
  tier is **properly manned**: the appropriate number of sibling formations are present, each
  at **≥ 30% of its authorised (operative) size**.
* This applies to **every** rung, including the first (Rank 1 → 2) — the rule is the same at
  all tiers. A lone or under-strength company earns nothing; you can only grow an officer as
  fast as you are willing to field — and man — the larger formation around him.
* Under-strength, empty, or lone formations stop earning.

### Combat Experience conversion ("the academy")

`Combat Experience` can be **converted** toward officers. An entire `Company` or `Unit` can
be sent "to the academy" via a button; a percentage then emerge at various ranks. Higher
`Combat Experience` raises the likelihood of higher ranks. This does not necessarily
*complete* their training, but can reduce it.

Training higher ranks is slower / costlier: you might generate 3,000 research points per
turn but the military academy may only spend 100 per turn; a general may take 500.

---

## Per-rank population cap (Promotion Ceiling)

To stop every officer becoming a General, the number of officers of **each rank** a player
may hold at once is capped. The cap scales with the size of the army actually fielded, plus
a few per-player perks:

```
maxOfficers(rank) = floor(totalSoldiers / divisor[rank]) + perkBonus[rank]
```

* `divisor[rank]` grows steeply with rank (Lieutenants plentiful, Generals rare).
* Rank 1 is effectively uncapped / always available.

### Cap collision → stall & bank XP

When a player is at the cap for a rank and an officer becomes experience-ready to ascend, the
officer **stalls at his current rank, banks the experience, and auto-promotes the instant a
slot at that rank frees up**. (Flagged for future review — see below.)

---

## Structural concepts, tiers and their labels

Two kinds of names are in play, and it matters which is which:

* **Structural concepts** — *Unit* and *Command* — are first-class game objects that exist no
  matter what anything is labelled.
* **Tier labels** — *Company*, *Platoon*, *Squadron*, … — are **cosmetic and fully
  customisable** names for the tiers *inside* a structure. Every label in this document is only
  an **example default**; the player may rename any tier, per structure, in the Unit Designer.

### Structural concepts (not labels)

**Unit** — a persistent body of soldiers of the same components, faction, training time,
location and authorised organisation; one instance built from a Unit Template. Its internal
shape *is* the tiers of the OrganisationStructure the template points at.

* Has a Unit Commander and one or more bottom-tier formations.
* **Authorised Strength** — set at creation, bounded by the tier floor and rank ceiling.
* **Current Strength** — falls with casualties.
* **Initial Unit Strength** — *(Commissioned only)* the size at creation; the maximum the Unit
  can ever have. A Unit created with 1,000 men that loses 200 still has an Initial Unit
  Strength of 1,000. This models a bloodied Unit being reinforced back up to — but never
  beyond — its authorised strength. **Unled units are not bound by it**: they are bounded by
  their tag's `Max` instead, so a split-off settler group can later merge back up to full size
  (see [Splitting and merging](#splitting-and-merging)).
* Bottom-tier formations persist through casualties, are not recalculated automatically, and
  may merge or disband during reorganisation (disbanded, merged, leaderless, or critically
  depleted).

**Command** — a collection of one or more `Units` operating under a shared command structure on
the same Campaign Tile. This is the layer that consumes the **(max tiers)+1** top rank.

* Has one Command Commander, whose rank sets the **max total strength**, **max number of
  Units**, and **max attached Specialists**.
* A Command's `Sight` is the **maximum** `Sight` of the Units within it — the best-placed pair of eyes
  sees for the whole Command, rather than their ranges being added together. See
  [Vision on the campaign map](../campaign-map/vision.md) for the full set of rules.
* `Detection` sharing within a Command is designed but **not yet implemented** — Detection currently
  plays no part in what a player can see.

**Unit Stack** — multiple `Units` on the same Campaign Tile that are **not** combined into a
`Command`.

### Example tier labels (customisable per structure)

The tiers inside a Unit are cosmetic-labelled. Suggested defaults by movement domain for a
full 5-tier structure — rename freely:

| Tier | Land (example) | Sea (example) | Air (example) |
| --- | --- | --- | --- |
| 1 | Company | Ship | Flight |
| 2 | Platoon | Squadron | Squadron |
| 3 | Battalion | Flotilla | Wing |
| 4 | Regiment | Task Group | Group |
| 5 | Division | Fleet | Air Command |

A structure with fewer tiers simply uses fewer labels — a 2-tier militia structure might be
*Band → Warband*, for instance. (Unled tags have no tiers and therefore no tier labels.)

---

## Splitting and merging

Splitting is a **player action, available at any time in the field** — not only at creation.

### Commissioned

Splitting detaches **whole formations along tier lines**; the detachment takes its officer
with it. The legal split points are exactly the structure's tiers — a Division can release a
Regiment, a Regiment a Battalion, and so on. (This is unchanged behaviour, recorded here for
the first time.)

### Unled

Splitting peels off **quanta**, subject to one invariant:

> Every resulting group is at least one quantum (`Base`). All but one of the groups are exact
> multiples of `Base`; one group carries the remainder.

Worked example — a 90-strength settler unit with `Base = 25` can split at most
`floor(90 / 25) = 3` ways. Legal outcomes are `25 + 25 + 40`, `25 + 65`, `50 + 40`, or no
split. A lone 15 never exists — the remainder always rides with a group. A unit below one
quantum cannot split at all.

This is the mechanic behind the settler decision: a 200-strong party can found one
200-resident city, split into two 100s for two towns, or peel off 25s for work camps.

### Merging

Unled units of the same tag on the **same tile** may merge, up to the tag's `Max` — so
splitting is fully reversible (100 + 100 → 200), and ragged remainders can recombine into
clean multiples (40 + 35 → 75). Commissioned merging remains governed by the reorganisation
rules under [Structural concepts](#structural-concepts-not-labels).

---

## Worked examples

### Infantry (default for a human Body) — 5 tiers

Structure sizes: **10 / 50 / 100 / 500 / 2000**. Positional mapping to rank, all operative
sizes sit under their rank ceiling (leaving room to adjust up):

| Tier | Example label | Operative size | Rank | Rank ceiling |
| --- | --- | --- | --- | --- |
| 1 | Company | 10 | 1 | 44 |
| 2 | Platoon | 50 | 2 | 200 |
| 3 | Battalion | 100 | 3 | 1,000 |
| 4 | Regiment | 500 | 4 | 3,200 |
| 5 | Division | 2,000 | 5 | 10,000 |

**Officer requirement by Unit depth** (tier-1 officers are free):

| Depth | Total men | Officers by tier (T1 → T5) | Total | Free (T1) | Earned |
| --- | --- | --- | --- | --- | --- |
| Tier 1 | 10 | 1 | 1 | 1 | 0 |
| Tier 2 | 50 | 5 + 1 | 6 | 5 | 1 |
| Tier 3 | 100 | 10 + 2 + 1 | 13 | 10 | 3 |
| Tier 4 | 500 | 50 + 10 + 5 + 1 | 66 | 50 | 16 |
| Tier 5 | 2,000 | 200 + 40 + 20 + 4 + 1 | 265 | 200 | 65 |

A maxed 5-tier Infantry Unit therefore needs **265 officers**, of which **200 are free**
(the bottom tier) and **65 must be earned** through combat and ascension.

### Settler (via a `camping-goods` component override) — Unled

The same human Body, but the `camping-goods` component swaps the tag to `Settler`, which is
**Unled** with authored sizes:

| Base (quantum) | Default | Max |
| --- | --- | --- |
| 25 | 200 | 5,000 |

**Zero officers**, at any size. Legal sizes are any multiple of 25 up to 5,000; a unit
created without an explicit choice is 200 strong. The payoff is the settlement decision: one
200-resident city, two 100-resident towns, or smaller work camps split off in the field —
and, because unled units merge freely up to `Max`, the choice stays open until the moment of
settling.

### Scout, sniper, drone — Unled individuals

The cases that motivated the unled model: templates fielding one person (or machine), where
an officer in the chain is pure noise.

| Tag | Base | Default | Max |
| --- | --- | --- | --- |
| Scout | 1 | 1 | 1 |
| Sniper | 1 | 1 | 2 |
| Drone | 1 | 1 | 4 |

A `Max` above 1 (a sniper pair, a drone flight) allows a small pooled group that still has no
officers, no hierarchy and no tier structure — it splits and merges by ones.

---

## Future review

**Banked-XP promotion surge** (from the "stall & bank XP" cap-collision rule).
When a player is at their per-rank officer cap, an experience-ready officer stalls, banks the
experience, and auto-promotes when a slot frees.

* **Pros:** no wasted progress; forgiving; rewards expansion (grow the army → caps rise →
  your veterans finally earn their stars); minimal micromanagement.
* **Cons:** enables a *promotion surge* — bank experience-ready officers during peacetime,
  then expand territory/army to lift every cap at once and cash in a wave of instant
  high-rank officers, potentially swinging a war right after a conquest. Also makes the cap
  feel "soft".
* **Mitigations to weigh:** cap banked experience (e.g. at most one rank's worth), decay
  banked experience over time, or throttle promotions-per-turn.

**Overload** (parked). A commander taking responsibility for another's orphaned soldiers when
that officer dies — intentionally left out of the current model; revisit later.

**The armed-settler hole** (known open risk — accepted for now). Because org tags derive from
components, nothing stops a design of human body + `camping-goods` + swords + armour: it
derives the `Settler` tag, is Unled, and can be 5,000 strong with **zero officer
requirements** — sidestepping the officer economy. Banning weapons on unled templates is not
an option (a sniper is unled *and* armed). The only committed guardrail today is the
tag-authored `Max`. Candidate mechanical levers, deliberately **not** committed yet:

* **Unled units cannot join a `Command`** — no shared vision/detection, no command bonuses.
* **A leaderless combat malus** — unled units attack/defend at a penalty (number to be set
  with combat resolution, alongside the existing 30% out-of-supply values).
* **No veterancy growth / no academy feed** — closes the indirect officer-farm route, at the
  cost of ruling out things like a veteran sniper corps.

**Unled units and the promotion ceiling.** Do unled soldiers count toward `totalSoldiers` in
`maxOfficers(rank) = floor(totalSoldiers / divisor[rank]) + perkBonus[rank]`? If they do,
settler spam inflates the officer cap — the same exploit shape as the rejected officer farm.

**Unled units and Combat Experience.** Command Experience is settled (unled units earn none).
Combat Experience is a separate currency: if unled units earn it, the academy conversion lets
a veteran scout corps feed the officer pool indirectly. Feature or leak — undecided.

**Compiled snapshots are replaced on recompile.** A fielded unit references its template's
compiled snapshot, which is overwritten when the template is recompiled — so fielded units
silently track template edits. That collides with "existing units keep working exactly as
they are" (see the obsolescence design). An obsolescence-era problem (snapshot versioning),
noted now rather than solved now.

**Promotion ceiling basis.** Cap is currently `floor(totalSoldiers / divisor[rank]) + perks`.
Consider whether settlement count or other factors should also feed it.

---

## Open TODOs

<span style="color:orange;">TODO: Units (up to the applicable quantity) of domain Land or Sea may be on the Water domain provided there are Units carrying a respective `LAND_CARRYING_CAPACITY` or `AIR_CARRYING_CAPACITY`.

<span style="color:orange;">TODO: Unit Design: component production time.

<span style="color:orange;">TODO: Material Class. When designing a Unit you choose which **Class** of component to equip (e.g. you choose to give a
soldier a sword). The *concrete* type of sword (broadsword, short sword, katana) is decided **at
creation time** from available assets, ordered by the player's [Material Preference](../production-and-assets.md).

<span style="color:orange;">TODO: Obsoleting a component check. When a component is made obsolete by a new technology discovery, it is no longer used in automatically generated unit designs. Existing units keep working and may upgrade. When **all**
components of a unit are obsolete, the unit design itself is obsoleted.

<span style="color:orange;">TODO: When a unit has multiple weapons, how is partial ammunition availability handled?

TODO: Integrated design with [Infrastructure](../campaign-map/infrastructure.md).</span>
