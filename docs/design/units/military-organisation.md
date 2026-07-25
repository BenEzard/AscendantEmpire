# Military Organisation

## Overview — the model at a glance

Military organisation is built from a few concepts that are **separate but associated**.
The two that are easy to conflate — *rank* and *organisation* — are deliberately kept
apart and joined by a single positional rule:

* **OrganisationStructure** — the *shape* of a formation: an ordered list of 1–5 tiers,
  each with an authored size. Reusable; owned by nothing in particular.
* **MilitaryOrganisation tag** — a *pointer* to one OrganisationStructure. A tag is just a
  named handle; the structure it points at is the real definition.
* **Rank (OfficerClass)** — the *seniority* of an individual officer. Supplies a **hard
  ceiling** on formation size and a cap on attached specialists. It never defines a
  formation's size — it only limits it.
* **Officer** — a person with a permanent movement domain and a rank, grown from combat.
* **Population cap** — how many officers of each rank a player may hold at once.

The join between shape and seniority is **positional**: *tier N is commanded by a Rank-N
officer*. Because tiers ascend, chain-of-command seniority is automatic — a commander
always outranks the tier directly beneath him.

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
tall: five ranks for the tiers of a maximally-deep formation, plus one more for the single
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

Two different numbers, deliberately stored in two different places:

| Number | Lives on | Player-editable? | Role |
| --- | --- | --- | --- |
| **Operative size** | the structure's **tier** | yes | what this formation actually holds |
| **Hard ceiling** | the **rank** | no (fixed) | the most a tier at that rank may be set to |

The **constraint is enforced against the tier** but the **ceiling value lives on the rank**:

```
structure.tier[N].operativeSize  ≤  rank[N].maxSoldiers
```

The player may adjust a tier's operative size **within the range** up to its rank ceiling.
The ceiling must live on the rank (which the player cannot edit per-structure) — if it lived
on the tier, a player could simply raise it, and the guardrail would mean nothing.

* **Floor:** a tier must contain a whole number **≥ 2** of the formations beneath it — a
  fan-out of 1 would just be a pointless duplicate of the tier below. So the smallest legal
  size for tier N is **2 × (tier N-1)**, and any operative size is a multiple `m × size[N-1]`
  with `m ≥ 2` that still fits under the rank ceiling.
* The old `UnitComponentBody.MinimumUnitSize` / `MaximumUnitSize` fields are **removed** —
  size bounds now come from the structure tier (floor) and the rank ceiling (cap).

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
officer corps is the scarce, strategic resource.

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
formation in action long enough for an officer to climb all five rungs, backfilling at the
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

## Formations, tiers and their labels

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
* **Initial Unit Strength** — the size at creation; the maximum the Unit can ever have. A Unit
  created with 1,000 men that loses 200 still has an Initial Unit Strength of 1,000.
* Bottom-tier formations persist through casualties, are not recalculated automatically, and
  may merge or disband during reorganisation (disbanded, merged, leaderless, or critically
  depleted).

**Command** — a collection of one or more `Units` operating under a shared command structure on
the same Campaign Tile. This is the layer that consumes the **(max tiers)+1** top rank.

* Has one Command Commander, whose rank sets the **max total strength**, **max number of
  Units**, and **max attached Specialists**.
* Units in a Command share `Detection` and `Sight` information.

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

A structure with fewer tiers simply uses fewer labels — a 2-tier Settler structure might be
*Party → Caravan*, for instance.

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

**Officer requirement by formation depth** (tier-1 officers are free):

| Depth | Total men | Officers by tier (T1 → T5) | Total | Free (T1) | Earned |
| --- | --- | --- | --- | --- | --- |
| Tier 1 | 10 | 1 | 1 | 1 | 0 |
| Tier 2 | 50 | 5 + 1 | 6 | 5 | 1 |
| Tier 3 | 100 | 10 + 2 + 1 | 13 | 10 | 3 |
| Tier 4 | 500 | 50 + 10 + 5 + 1 | 66 | 50 | 16 |
| Tier 5 | 2,000 | 200 + 40 + 20 + 4 + 1 | 265 | 200 | 65 |

A maxed 5-tier Infantry formation therefore needs **265 officers**, of which **200 are free**
(the bottom tier) and **65 must be earned** through combat and ascension.

### Settler (via a `camping-goods` component override) — 2 tiers

Structure sizes: **25 / 100**. The same human Body, but a different structure via the tag
override:

| Tier | Operative size | Rank | Rank ceiling |
| --- | --- | --- | --- |
| 1 | 25 | 1 | 44 |
| 2 | 100 | 2 | 200 |

A full 100-man Settler formation = four tier-1 groups + one tier-2 commander = **5 officers**,
of which **4 are free** and **1 is earned**.

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

**Promotion ceiling basis.** Cap is currently `floor(totalSoldiers / divisor[rank]) + perks`.
Consider whether settlement count or other factors should also feed it.

---

## Open TODOs

<span style="color:orange;">TODO: Units (up to the applicable quantity) of domain Land or Sea may be on the Water domain provided there are Units carrying a respective `LAND_CARRYING_CAPACITY` or `AIR_CARRYING_CAPACITY`.

<span style="color:orange;">TODO: Unit Design: component production time.

<span style="color:orange;">TODO: Material Class. When designing a Unit you choose which **Class** of component to equip (e.g. you choose to give a
soldier a sword). The *concrete* type of sword (broadsword, short sword, katana) is decided **at
creation time** from available assets, ordered by the player's [Material Preference](production-and-assets.md).

<span style="color:orange;">TODO: Obsoleting a component check. When a component is made obsolete by a new technology discovery, it is no longer used in automatically generated unit designs. Existing units keep working and may upgrade. When **all**
components of a unit are obsolete, the unit design itself is obsoleted.

<span style="color:orange;">TODO: When a unit has multiple weapons, how is partial ammunition availability handled?

TODO: Integrated design with [Infrastructure](campaign-map.md#infrastructure-terrain-improvements).</span>
