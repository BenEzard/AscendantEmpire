# Departments (Advisors)

## Concept

Other games in this genre have **Advisors** that provide functionality and information displays
(Research, Domestic Affairs, Foreign Affairs). Ascendant Empire expands this into a more integral,
**evolving** system: **Departments**.

- At the start of the game, Departments **don't exist**. They can only be created after technological
  progress and paying upfront establishment costs, plus an ongoing cost to support the bureaucracy.
- A new Department starts as a **single position** occupied by a [Specialist](specialists.md).
- Through technology and expenditure, the player can **expand** a Department — adding positions that
  provide more functionality and capabilities. (E.g. filling a position in the Research Department
  reminds the player when they're not actively researching, saving research time.)
- A position can be filled by **various types of specialists**, each conferring different bonuses to
  the empire.
- Specialists in a Department gain experience and become more effective over time — making them
  attractive targets for espionage and assassination.

## Definitions

- A **Department** has a **title** that evolves over time.
- A Department has **1 or more Divisions**; their number and names evolve. A Division is a logical
  separation, primarily for the UI (e.g. a Defence department might have "Homeland Security", "Army
  Operations", "Navy Operations").
- Each Division has **1 or more Positions**, where a specialist can be assigned a role. A Position
  has a title (e.g. "Head Engineer").
- Positions are **locked** by default and made available by paying an establishment cost or via a
  technological discovery.
- Each Position has a **Position Constraint** dictating which specialists are eligible and the
  benefits they confer when filling it.
- Each Division has a **Division Constraint** limiting the type and number of specialists at once
  (e.g. no more than 2 spies at a time).
- Over time, the position name, constraint, and costs can change.

> Open question: should ongoing costs differ depending on the specialist? (Leaning yes.)

## Design notes

**Department**
- Properties: `Name`, `Division Count`, `Ongoing Cost` (compiled — recomputed whenever a specialist
  is added/removed, a division is added/removed, or a position is unlocked/filled/vacated).
- Methods: `Rename`, `Add Division`, `Remove Division`, `Rename Division`.

## Science Advisor

An example Department. Possible positions:

- **Clerk** — tracks which cities produce the most research output.
- **Research Coordinator** — enables [research overflow](technology.md#research-overflow).
- *(unnamed)* — set projects for unused experience points.
- *(unnamed)* — set projects for unused diffusion points.
- *(unnamed)* — set projects for unused research points.
- **Efficiency Expert** — warns when a technology will likely complete next turn and no follow-up
  has been selected.
- **Research Strategist** — lets the player select what will be studied next.
