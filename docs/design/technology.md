# Technological Development

## The three-bucket model

Each technology advancement has between **1 and 3 buckets** of research:

- **Directed Research** — active research; a conscious decision to pursue a line of study.
- **Experience-based Research** — passive; a byproduct of unit/population actions.
- **Diffusion-based Research** — passive; a byproduct of trade and proximity to other empires.

Each bucket must be **completely filled** for the technology to be discovered. Different technologies
require different amounts per bucket. For example:

| Tech A | Directed Research | Experience-based | Diffusion-based |
| --- | --- | --- | --- |
| requirement | 500 | 250 | 250 |

Tech A needs **1,000 points total**: at least 500 directed, with experience and diffusion capped at
250 each.

A technology uses a bucket or it does not. An unused bucket has **no requirement at all** rather than a
requirement of zero, so "does this technology need experience?" and "how much?" are the same question.

### Locked, available, unlocked, discovered

A technology is in exactly one of four states, per player:

- **Locked** — its prerequisites are not yet satisfied. It accumulates nothing.
- **Available** — every prerequisite is satisfied, but the acquisition cost has not been paid. The
  experience and diffusion buckets fill; directed research cannot begin.
- **Unlocked** — prerequisites satisfied *and* the acquisition cost paid. All three buckets fill, and the
  technology may be chosen as the directed research project.
- **Discovered** — every required bucket is full. Its grants have been applied and it absorbs nothing more.

The acquisition cost is **zero or more Assets with a quantity**. Most of the early tree is free, and a
free technology moves straight from available to unlocked.

A prerequisite can be **mandatory** or **optional**. To become available, **all** mandatory prerequisites
must be discovered (if any) **and** at least one optional prerequisite (if any). A technology with no
prerequisites is available from the first turn.

## Seven methods of technological advancement

Technologies may be uncovered through a combination of these seven methods.

1. **Experience-based research** — passive improvement from doing things. Unit/population actions
   (exploration, hunting, farming, mining) and manufacturing build related tech (e.g. the more
   submarines you build, the more submarine tech unlocks). The more you do something, the better
   you get.
2. **Diffusion-based research** — passive gathering of information through trade partners and
   proximity. Any trade agreement starts a diffusion trickle (military/non-military); tech-sharing
   offers automatically grant diffusion points toward that tech; settlements near another empire
   accumulate diffusion points.
3. **Directed research** — the main method; a deliberate line of research. It can *fully* complete a
   technology without experience or diffusion. It cannot begin until the technology's acquisition cost is
   paid.
4. **Trade** — technologies can be traded between empires (one or more at a time). When traded, the
   receiving empire is topped up to 100% and any research cost is marked paid. (Due to tech
   randomisation, the points given may differ from the points received.)
5. **Espionage** — technologies, or portions of them, can be stolen. Success is affected by tech
   development, specialists, and the victim's counter-intelligence.
6. **Battlefield capture** — capturing or killing an enemy unit may yield points toward tech.
7. **Conquering** — conquering settlements grants access to some of the defeated empire's tech, but
   the conqueror may not fully understand it and may still need to research it. This can be improved
   via tech and specialists in [Departments](departments-advisors.md). A soon-to-be-conquered empire
   may **destroy its own city** to minimise theft (at the cost of social upheaval). Conquering a
   **capital** steals *all* technological knowledge (including partially researched techs).

Of these, only the first three produce points of their own. The last four move points or whole
technologies between players, and each records **who it came from** as well as how.

## How experience is earned

Experience is a **typed currency**, not a single pool. The three buckets are not symmetrical: directed
points are a budget a player spends freely, but an experience point arrives already attached to the kind of
activity that earned it.

Each kind is an **experience type** — "Jungle", "Mining", "Stone Cutting", "Sword Making", "Exploration".
Producing systems earn them by doing their own work:

- A Force moving onto a jungle tile earns Jungle experience; a settlement founded on arid ground earns
  Arid experience.
- A unit uncovering terrain no one has seen earns Exploration.
- A settlement or camp extracting from a tile earns the matching extraction type — foraging, hunting,
  mining, woodcutting — **in proportion to how much was taken**, so a rich seam teaches more than a poor
  one.
- A production process turning one Asset into another earns the matching refining type.
- Producing equipment or units earns the matching manufacture type; keeping animals earns husbandry.

How many points an action is worth is decided by the action, and may differ per player. A technology
declares only **which types feed it**, never how much they give.

At the end of a turn each type's total is **split evenly** between the player's technologies that consume
it and can still absorb it. Anything left over — the remainder of an uneven split, a recipient already at
its requirement, or a type no technology of theirs consumes — becomes **unused experience**. Unused
experience is never spent and never applied to a technology discovered later; it accumulates only so
players can be compared on it.

## Technology settings (per game)

- **Technology Speed Multiplier** — multiplies the research required. E.g. `1.5` makes technologies
  take 50% longer.
- **Technological Fuzziness** — e.g. `5` means a technology may be adjusted by anywhere from −5% to
  +5%. Applied across all technologies, shared equally across the three buckets.
- **Technology Customisation:**
  - `IDENTICAL` — requirements are the same for all empires (only the speed multiplier applies).
  - `RANDOMISED_EQUAL` — requirements are randomised, then balanced per era so empires are within 1% of
    total. Balancing per era rather than on the grand total is what stops one player being cheap at one
    end of the tree and ruinous at the other.
  - `FULLY_RANDOMISED` — requirements are fully randomised, with no re-balancing.

Each player's requirements are worked out **once, at game setup**, and stored against them. Changing a
setting afterwards has no effect on a game already created.

## How advancement works

Advancement (active or passive) can only occur on technologies that are at least **available** —
experience and diffusion fill from that point, directed research from **unlocked**.

### Research overflow

Overflow occurs when the currently researched technology has fewer requirements remaining than the
points available to apply. Example: Tech A (current) needs only 70 more directed points; Tech B is
unlocked; 300 directed points are available this turn.

| | Tech A (current) D / E / Diff | Tech B (unlocked) D / E / Diff |
| --- | --- | --- |
| remaining required | 0 / 0 / 70 | 1,500 / 250 / 56 |

After the 70 points finish Tech A, **230 points remain**, and overflow lets them be spent on any
unlocked technology.

- Overflow applies to **Directed Research only when the acquisition cost has been paid**. Experience
  and diffusion research occur regardless of acquisition cost.
- If overflow is **not** enabled, any excess research points for the turn are either returned or
  lost.

### Unspent research points

Where research points are unspent (no project selected, or no unlocked technologies available), they
may be lost or returned. Points are **segmented by type** (directed, experience, diffusion). Unspent
points can be directed to **Research Projects**, set via the [Science Advisor](departments-advisors.md#science-advisor).

Example research projects:

- **Quality Craftsmanship** — convert unspent *experience* points into culture points.
- **Cultural Richness** — convert unspent *diffusion* points into money.

### Specialists

Research points can also be spent training specialists. For example, a **Research Coordinator**
(Science Advisor) enables research overflow.

## Turn order (research)

- **Start of turn:** check whether the current research project has completed; if so, prompt the
  player to select a new one (or auto-select a pre-chosen secondary project). Calculate research
  points generated this turn.
- **End of turn:** distribute the experience earned during the turn, then spend remaining research points
  on the selected/unlocked technologies (the player may have spent some during the turn).

```
Research Points Generated per Turn = [Setting: EMPIRE_BASE_RESEARCH_POINTS] + …
```

---

# Technical details

## Data model

Everything lives in the `technology` schema, except the per-game settings, which sit beside `GameIndex` in
`game`.

### Catalogue

Authored content, shared by every game and every player. Each table's match key is `Code`, so re-seeding
renames a row rather than duplicating it.

| Table | Description |
| --- | --- |
| `technology.Technology` | One researchable technology: code, name, icon, era, optional tree position, and the **base** requirement for each of the three buckets (null where the bucket is unused). |
| `technology.TechnologyAcquisitionCost` | Zero or more Assets, with a quantity, that must be paid to unlock the technology. A real foreign key to `assets.Asset`; no rows means free. |
| `technology.TechnologyPrerequisite` | The prerequisite graph. Self-referencing many-to-many, so the tree branches and merges, with a `Mandatory` / `Optional` kind per edge. |
| `technology.TechnologyGrant` | What discovering a technology unlocks or retires: `(TargetType, TargetId)` plus an `Unlock` / `Obsolete` effect. |
| `technology.ExperienceType` | The kinds of experience an action can earn, each with a code producing systems resolve their own concept against. |
| `technology.TechnologyExperienceType` | Which experience types feed which technology. A mapping only — it carries no quantity. |

`Technology.Era` has exactly one consumer: the `RANDOMISED_EQUAL` generator, which balances per era. It is
not a UI grouping, and the tree has no branch or category dimension — its shape is the prerequisite graph
and its layout the optional `TreeColumn` / `TreeRow`.

`TechnologyGrant.TargetId` carries **no foreign key**, because it points at a different table for each
target type — an Asset, an Asset meta-category, a unit component, a unit template or a map resource type.
The upsert validates that it resolves; nothing re-checks it afterwards, so a target deleted later leaves
the grant dangling, and the read reports that rather than hiding it.

### Per-player state

Every per-player table carries `GameId` as well as `PlayerId` — implied by the player, but carried so a
game delete can purge directly, and so "which other players in this game have X?" has a usable index.

| Table | Description |
| --- | --- |
| `technology.PlayerTechnology` | One player's progress toward one technology: status, **their own** three requirement figures, the three point totals, the rounds the cost was paid and the technology discovered, and how and from whom it was discovered. |
| `technology.PlayerResearchState` | One row per player: current and next research project, unspent directed and diffusion points, and the round the end-of-turn research step last ran. |
| `technology.PendingExperienceAccrual` | Experience earned during a turn, accumulated per type, awaiting distribution. Empty between turns. |
| `technology.PlayerUnusedExperience` | Experience that had nowhere to go, per type. Never spent. |
| `technology.PlayerResearchModifier` | Modifiers to how many points a player generates per turn, by bucket and granting source. Mirrors `PlayerSustenanceModifier`. |
| `technology.ResearchHistory` | Per-round summary by bucket — generated, spent, overflowed, lost. Append-only. |

A `PlayerTechnology` row exists for **every player and every technology from game setup onwards**, so the
presence of a row says nothing: the question is always `Status`. This differs from `PlayerUnlockedAsset`,
where the row's existence *is* the unlock, and an availability check must not be copied across.

Rows exist up front because the requirement figures are materialised per player, and `RANDOMISED_EQUAL`
can only balance with the whole set in hand. Under `IDENTICAL` every player's figures match.

`Status` is a **materialised cache**. Locked versus available is a pure function of the prerequisite edges
and the player's discovered set, recomputed by one owner after any discovery, at start of turn, and after
any seed that touched the catalogue. That last trigger matters: seeding a new technology leaves every
player in every existing game with no row for it, so the reconcile backfills rows before recomputing
statuses.

### Per-game settings

`game.GameConfiguration` shares its primary key with `GameIndex` and is created with it. It holds the base
research points per turn, the speed multiplier and fuzziness as scaled percentages, the customisation
mode, whether overflow is enabled, and whether unspent points are lost or returned.

`GameIndex` keeps the settings it already has — battle map size and the wrap flags. Game settings
therefore live in two places, so where a new one belongs is a question to ask rather than assume.

## API

One controller owns `/technology`. Experience types are a nested resource on it rather than a prefix of
their own, since they exist only to feed technologies.

| Endpoint | Authorization | Purpose |
| --- | --- | --- |
| `GET /technology` | Any logged-in user | The whole catalogue, with each technology's costs, prerequisites, grants and experience sources. Grant targets are resolved to a code and display name server-side. |
| `PUT /technology` | `EntitySetupTool` | Upsert technologies and their four child collections, matched on `Code`. |
| `GET /technology/experience-types` | Any logged-in user | Every experience type, in display order. |
| `PUT /technology/experience-types` | `EntitySetupTool` | Upsert experience types, matched on `Code`. |

The catalogue read is player-independent while customisation is `IDENTICAL`, so a client may hold it for
the life of a game. It serves the **catalogue's** base requirements, never a player's.

A technology's four child collections are **replaced wholesale** by the upsert, not merged, so an omitted
collection clears it. Two things the upsert checks that the database cannot: that the prerequisite graph
stays **acyclic**, and that every grant target and cost Asset **resolves**.

## Seed data

Two seed targets, `ExperienceTypes` and `Technologies`, the second depending on the first. Both run
through the same upsert the Entity Setup Tool uses, so seeding and hand-editing cannot diverge.

The technology catalogue is **generated from the Tech Map sheet of `AscendantEmpire_Files/Resources/Tech.xlsx`**
by the scripts in [`tools/tech-seed/`](../../tools/tech-seed/README.md) — each box is a technology, each
arrow a prerequisite, a dashed arrow an optional one. That README records what the diagram does not carry
and what is assumed instead, and the generated file's own remarks repeat it.

## Open questions

- Should capturing a technology reveal the *entire* tech, pay the full research cost, or just the
  majority (e.g. 80%)?
- Should tech trading be allowed when the recipient is too far behind to understand it?
- Which era does each technology belong to? The source diagram carries no era dimension, so everything is
  currently `Stone`.
- What should the research requirements actually be? They are currently derived from depth in the tree as
  a placeholder.
- Which technologies grant which Assets, components and templates? The diagram's annotations name things
  that mostly do not exist in those catalogues yet, so they are carried as description text rather than as
  grants.
