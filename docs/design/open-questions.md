# Open Questions and Unorganised Thoughts

A catch-all for cross-cutting design questions and rough notes that don't yet belong to a settled
area. Topic-specific open questions live at the bottom of their own documents (e.g.
[technology.md](technology.md#open-questions),
[units/unit-mechanics.md](units/unit-mechanics.md#open-questions)).

## Biome classification

The biomes are simplified from the **Olson & Dinerstein (1998)** WWF / Global 200 classification
([overview](https://en.wikipedia.org/wiki/Global_200),
[map](https://en.wikipedia.org/wiki/Global_200#/media/File:Ecoregions.png)), which has too many
entries to be practical. The simplification:

| Original classification | Simplified to |
| --- | --- |
| Lake | Lake |
| Ice and snow | Polar |
| Tundra | Tundra |
| Montane Grasslands & Shrublands | Grassland |
| Boreal Forests & Taiga | Forest |
| Temperate Broadleaf & Mixed Forests | Forest |
| Temperate Grasslands, Savannas & Shrublands | Grassland |
| Flooded Grasslands & Savannas | Grassland |
| Mediterranean Forests, Woodlands & Scrub | Woodland |
| Mangroves | Mangrove |
| Tropical & Subtropical Dry Broadleaf Forests | Forest |
| Tropical & Subtropical Moist Broadleaf Forests | Forest |
| Tropical & Subtropical Grasslands, Savannas & Shrublands | Grassland |
| Tropical & Subtropical Coniferous Forests | Forest |
| Deserts and Xeric Scrublands | Desert |

## Medical incidents

If random events are enabled, settlements may periodically get **sick**. An illness can be cured
within 2 turns (by spending Tribal Medicine, scaled to the settlement's population), or a percentage
of the population dies.

- This opens another form of early-game interaction with nearby NPC villages ("we're sick, can you
  help?").
- It would be good if some illnesses could be **spread by unit movement**.

## Battles — discipline and morale

Units have a **morale** and a **discipline** stat:

- **Discipline** is used when **defending** (e.g. it lets defenders target specific enemies with
  fortifications — see [battlefield-maps.md](battlefield-maps.md#fortifications)).
- **Morale** is used when **attacking**.

## Combat domains and unit capabilities

A cluster of unresolved questions around how attacks work across domains and across a turn:

- How far a unit moves on the battle map is settled — see [unit-movement.md](units/unit-movement.md#battle-movement).
- Can a unit attack **and** move in the same turn? Some units (velites with a throwing spear) should
  attack once and move once; some (artillery) should move *or* fire, not both. How many times can a
  unit attack per turn?
- Which **domains** can a unit attack, and how strongly? (A machine gun should damage a plane, but
  not much.) Illustrative units: caveman with a stick; velites (attack once, move once); archer;
  sniper; tank; anti-aircraft gun (ground + air); submarine.
- **Travel domain vs battle domain** — these are separate. E.g. an anti-aircraft gun sits on a
  LAND-based chassis, but the gun itself attacks in the AIR domain. Leaning toward: an attacker's
  weapon must be in a battle domain equal to the defender's *travel* domain, while **defence values
  ignore domain** (always apply).
- Edge cases still to work out: most ship attacks against submarines should be weak unless they have
  anti-submarine weapons (depth charges); an aircraft with flares (missile distractors) should…
  *(unfinished)*.
- Unit Templates must remain **fully intact** with references to the `UnitComponent`, because a
  component's properties can be affected by bonuses (e.g. pike vs horse).

## Unit creation and promotion structures

- The total number of soldiers in a single Unit is determined by the commanding officer's rank (see
  [specialists.md](specialists.md#military-commanders)). When creating a unit you specify the
  commanding officer rank and the total number of soldiers. If the total exceeds the officer's
  effective command ability, the unit can still be created but suffers combat penalties.
- **Promotion structures** are generic per domain (land, sea, air) and can be further customised by
  the unit's chassis. They describe how many men are in a unit (e.g. 10), which sets the increment.
  Creating a unit of 10 uses **11 men** (1 officer). If the next promotion group is 30, then every
  30 men there are 33 (10 + 1 × 3) plus 2 officers over the top = 35. Each step up the hierarchy
  adds 1 officer. A unit can therefore be split at every 30 or every 10 — but 30 can't split into
  fewer than 3 groups.

## Other miscellaneous calculations

- *(Placeholder — the source had an empty "Calculations" section under Unorganised Thoughts.)*
