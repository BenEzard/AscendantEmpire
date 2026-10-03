# Ascendant Empire — Design

This folder captures the game design for **Ascendant Empire**, a turn-based 4X strategy game.
Each document covers one area of the design. The content here is the *design intent* — some of it
is settled, some of it is still a working sketch with open questions called out inline.

> These documents were distilled from the original `Empire.docx` design notes. Where the source
> raised a question or left something unresolved, it has been preserved (see
> [open-questions.md](open-questions.md) and the **Open questions** sections in each file).

---

## Contents

| Document | Covers |
| --- | --- |
| [design-principles.md](design-principles.md) | The guiding philosophy ("fun over accuracy"). |
| [starting-requirements.md](starting-requirements.md) | The catalogue content a database needs before a game can be created: the settler and scout tags, components and templates. |
| [turn-structure.md](turn-structure.md) | The parallel "turn-based" model, rosters, and how battle rounds interleave with campaign turns. |
| [production-and-assets.md](production-and-assets.md) | Assets, Material Classes, Material Preferences, luxury goods, and where Assets come from. |
| [units/military-organisation.md](units/military-organisation.md) | Ranks and organisation hierarchies, the officer economy, and how Units and Commands are sized and led. Supersedes the former `units-and-formations.md`. |
| [units/military-organisation-implementation-notes.md](units/military-organisation-implementation-notes.md) | The implementation notes and outstanding gaps behind the organisation model. |
| [units/unit-support-and-supply.md](units/unit-support-and-supply.md) | Supply chains and the four support types: fuel, ammunition, food, and medical. |
| [units/combat.md](units/combat.md) | How combat resolves between units. |
| [units/force-context-action-toolbar.md](units/force-context-action-toolbar.md) | The context action toolbar: how the backend decides what a selected Force can do, and how the frontend renders it. |
| [campaign-map/](campaign-map/README.md) | The campaign map, split by topic: tile geography and biome, depletion of a biome's yields, replenishment, food yield, the support indicator, MapResources and their discovery, vision (sight & detection), and infrastructure. |
| [intelligence-and-sightings.md](intelligence-and-sightings.md) | What a player learns while they are not looking: witnesses, tracks, fidelity, the report feed, and last-known-position intel. |
| [battlefield-maps.md](battlefield-maps.md) | How battlefield maps are generated, fortifications, battle flow, objectives, and manoeuvres. |
| [units/unit-movement.md](units/unit-movement.md) | The source of truth for unit movement: campaign stepping, tile costs, carry and replenishment, and battle movement. |
| [units/unit-mechanics.md](units/unit-mechanics.md) | Positioning/facing, tactics, fog of war, strength, and ranged attack. |
| [settlements.md](settlements.md) | Settlement types, asset banks, and buildings. |
| [specialists.md](specialists.md) | Specialists and military commanders. |
| [technology.md](technology.md) | The three-bucket research model, the seven methods of technological advancement, how experience is earned, and the schema and API behind them. |
| [foreign-relations.md](foreign-relations.md) | Inter-empire relationships and diplomatic arrangements. |
| [departments-advisors.md](departments-advisors.md) | Departments (the evolving "advisors" system). |
| [manufacturing-chains.md](manufacturing-chains.md) | Infrastructure manufacturing chains, with farm and blacksmith examples. |
| [early-game.md](early-game.md) | The nomadic early game and the path to settling. |
| [map-generation.md](map-generation.md) | Generating the campaign map from a source image. |
| [open-questions.md](open-questions.md) | Cross-cutting unresolved design questions and "unorganised thoughts". |

### Unit design

The unit design system — how a unit template is assembled from components — has its own folder.

| Document | Covers |
| --- | --- |
| [unit-design/unit-design-system.md](unit-design/unit-design-system.md) | Bodies, slots, components, and how a template is assembled. |
| [unit-design/tags-and-modifiers.md](unit-design/tags-and-modifiers.md) | Tags, tag types, and the modifiers they carry. |
| [unit-design/player-unlocks.md](unit-design/player-unlocks.md) | Which components and templates a player has access to. |
| [unit-design/unit-design-supercessions.md](unit-design/unit-design-supercessions.md) | How a design supersedes an earlier one, and what happens to units built from it. |

---

## Vocabulary at a glance

A few terms recur throughout these documents:

- **Empire** — a player (human or AI).
- **Asset** — anything collectable or manufacturable in the game, excluding Units. The catalogue is
  described in [assets-data-model.md](../assets-data-model.md).
- **Asset Bank** — where an empire's Assets are held, and what a cost is paid out of.
- **MapResource** — the extractable thing standing on a campaign tile (a berry patch, a herd of deer,
  a coal seam). Extracting one *yields* Assets; the two are never the same word.
- **Material Class** — a generic category of asset (e.g. `CONSTRUCTION_STONE`, `SWORD_MELEE`) that
  concrete assets belong to.
- **Unit** — one fielded body of soldiers with the same equipment, training and nationality, built from
  a single Unit Template.
- **Command** — one or more Units combined under a shared command structure on a single campaign tile.
  Always deliberate: a lone Unit is never automatically wrapped in one.
- **Force** — the umbrella term for either a Unit or a Command: whatever occupies a tile and moves as
  one. A category, not a type.
- **troops** — individual soldiers. A Unit contains troops; it is not one.
- **Campaign Map** — the strategic world map made up of tiles.
- **Battlefield Map** — the tactical map generated when a battle occurs on a campaign tile.
