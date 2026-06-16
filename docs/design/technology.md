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

### Discovered vs unlocked

- **Discovered** — a technology is discovered once *each* required bucket is full.
- **Unlocked** — a technology is "unlocked" when all prerequisite technologies have been discovered
  **and** its initial cost has been paid. Only unlocked technologies can accumulate research.

A prerequisite can be **mandatory** or **optional**. To unlock a technology, **all** mandatory
prerequisites must be complete (if any) **and** at least one optional prerequisite (if any).

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
   technology without experience or diffusion. It cannot begin until the technology's initial
   (acquisition) cost is paid.
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

## Technology settings (per game)

- **Technology Speed Multiplier** — multiplies the research required. E.g. `1.5` makes technologies
  take 50% longer.
- **Technological Fuzziness** — e.g. `5` means a technology may be adjusted by anywhere from −5% to
  +5%. Applied across all technologies, shared equally across the three buckets.
- **Technology Customisation:**
  - `IDENTICAL` — requirements are the same for all empires (only the speed multiplier applies).
  - `RANDOMISED_EQUAL` — requirements are randomised, then balanced so empires are within 1% of
    total.
  - `FULLY_RANDOMISED` — requirements are fully randomised, with no re-balancing.

## How advancement works

Advancement (active or passive) can only occur on **unlocked** technologies.

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
- **End of turn:** spend remaining research points on the selected/unlocked technologies (the player
  may have spent some during the turn).

```
Research Points Generated per Turn = [Setting: EMPIRE_BASE_RESEARCH_POINTS] + …
```

## Data model

| Table | Description |
| --- | --- |
| `config.Technology` | All researchable technologies; stores the ratios between Research, Experience, and Diffusion. |
| `game.EmpireTechnology` | Generated technology values and their status, per empire. |
| `config.usp_Technology_Upsert`, `config.usp_TechnologyPrerequisite_Replace`, `config.usp_TechnologyResearchCost_Insert` | Config-time setup procedures. |
| `setup.usp_EmpireTechnology_Balance` / `_Grant` / `_Populate` / `_SetResearchCostPaid` | Per-game setup procedures. |
| `game.usp_EmpireTechnology_Get` / `_GetIDs` / `_GetResearchCandidates` / `_GetResearchCandidates_DiffusionTechs` / `_GetResearchCandidatesWithCost` / `_Update` | Runtime queries. |
| `game.usp_EmpireGeographyType_Get` | Geography lookup used during turn processing. |

## Open questions

- Should capturing a technology reveal the *entire* tech, pay the full research cost, or just the
  majority (e.g. 80%)?
- Should tech trading be allowed when the recipient is too far behind to understand it?
