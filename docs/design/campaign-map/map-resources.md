# MapResources

Part of the [Campaign Map](README.md) design.

A **MapResource** is something on a map tile that can be extracted: a berry patch, a herd of deer, a
coal seam, a forest's mix of woods. When extracted, it gives up **one or more Resources** (e.g. a
Berry gives `forage`; a Deer gives `medium_game` and `pelt`). A Resource is an Asset held in a
Resource Bank (see
[production-and-assets.md](../production-and-assets.md) and
[resources-data-model.md](../../resources-data-model.md)).

> **Naming.** Always write **MapResource** (and `MapResourceType`), never "resource", for the thing
> on the tile. "Resource" means the extracted Resource/Asset, and mixing the two is ambiguous.

MapResources appear on a `CampaignMapTile`.

> **Later: battle maps.** MapResources may also appear on battle maps (e.g. a berry patch or a stand
> of ebony on specific battle tiles). If we do this, battle map tiles will need a lot more work: they
> would have to carry their own MapResources, stay consistent with the campaign tile's MapResources,
> and interact with the battle map TERRAIN actions (clear forest, despoil forest) in
> [battlefield-maps.md](../battlefield-maps.md).

## MapResourceTypes

A `MapResourceType` (e.g. Berry, Deer, Horse, Coal Seam, Forest Wood) defines how its instances
behave. Its kinds are set by flags:

| Flag | Meaning |
| --- | --- |
| `Renewable` (bool) | Grows every round, and may spread. See [Renewable](#renewable). |
| `Stratified` (bool) | Not renewable. Held in layers, reached in order. See [Stratified](#stratified). |
| `Distribution` (bool) | Renewable, holds several Resources in set amounts, never spreads. See [Distribution](#distribution). |

- `Renewable` and `Stratified` are **mutually exclusive**, enforced by the MapResourceType validator.
- A `Distribution` must also be `Renewable`, and must not spread (no `SpreadLikelihood`).
- **Icon.** Most MapResourceTypes show an icon on the tile. Some, such as a Distribution of wood in a
  forest, have none: the biome already says "forest".
- **Yield.** Every MapResourceType gives up **one or more Resources**. It lists the Resources, and
  how much of each, that one unit extracted gives: a Berry gives one (`forage`); a Deer gives two
  (`medium_game` and `pelt`). A Distribution is different: it holds a separate amount of each of its
  Resources, and extracting it gives some of each (see [Distribution](#distribution)).
- **Extraction method.** Each MapResourceType has one: e.g. Gather, Hunt, Fell, Mine, or **Capture**
  (horses), which succeeds with a `CaptureLikelihood`. Extracting always reduces the quantity on the
  tile.
- **Research first (some MapResourceTypes).** A MapResourceType may require research before it can
  be extracted (e.g. Berry research before anyone in the empire can take berries). Others can be
  extracted as soon as they are discovered. See [Discovery](#discovery).
- **Each yielded Resource can be switched on and off.** Each Resource in a MapResourceType's yield
  can be switched on and off per Player by game mechanics (e.g. a tech). Extracting gives only the
  Resources that are switched on for that Player; the quantity on the tile still goes down by the
  full amount extracted. For example, once Deer is researched a Deer gives `medium_game` straight
  away, but gives `pelt` only after the Player unlocks the hide-working tech. Until then, the hides
  of deer taken are lost. This does not apply to a Distribution: all of its Resources are always
  given.

> Extraction itself is out of scope here, apart from the rules above and the Distribution rules
> below. In a Player's controlled area it is done by improvements
> (or from a nearby camp or Settlement, e.g. berries, without an improvement on the tile). In
> unclaimed or enemy land, a Force feeds from a food MapResource such as a berry patch: it is the
> `[Additional Resources]` term in the total-resource formula in [tiles.md](tiles.md).

## Placement on tiles

A tile holds **at most 2** MapResources.

Where a MapResourceType can exist, and its values there, are held **per MapResourceType, Biome and
Geography** (Geography nullable, meaning any Geography in that Biome; a row with a Geography wins
over one without):

| Value | Meaning |
| --- | --- |
| `InitialQuantity` range | The min and max quantity an instance is rolled with at game creation. |
| `MaximumQuantity` | The ceiling a renewable instance grows to. |
| `GrowthPercentage` | The share of its current quantity a renewable instance grows each round. |
| `SpreadLikelihood` | The chance each round that an instance at its ceiling spreads. |
| `SpreadLimit` range | The min and max `SpreadLimit` an instance is created with. |

**No row means non-compatible**: the MapResourceType cannot exist on that tile, and cannot spread
onto it. Berries have no Desert row and no water Geography rows, so they never grow there.

### Placing MapResources in the map editor

MapResources are placed on a gallery map's tiles in the map editor, which lists MapResourceTypes
grouped by **MapResourceCategory** (Food, Metals, Wood...). A category only arranges the editor's
list; nothing in play reads it.

Each game created from the map starts with an instance of every placement. Its starting quantity is
the one the editor set, if any; otherwise it is rolled from the `InitialQuantity` range when the game
is created. A Distribution instead starts at its scenario's amounts (the editor may pin a scenario,
otherwise one is picked as below), and a stratified MapResource's layers are always rolled.

### Player modifiers

On a **claimed** tile (within a Player's territory), the **claimant's** modifiers raise these values,
representing good management (e.g. from tech advances, different for each MapResourceType). Unclaimed
tiles use the table's values, whoever is on them. This matches how
[Replenishment](replenishment.md) treats claimed tiles.

Modifiers are held in their own table, in the same shape as `game.PlayerSustenanceModifier`: one row
per Player, source, MapResourceType, value changed (`MaximumQuantity`, `GrowthPercentage`,
`SpreadLikelihood`, and the `EstimationFuzzyFactor` and discovery likelihood of
[Discovery](#discovery)), Biome (nullable, meaning every Biome) and **Geography (nullable, meaning
every Geography)**, each with a `FlatValue` and a `PercentValue`. As with sustenance, a Player's
values are always derived from the modifiers they currently hold. For a value that is itself a
percentage, `FlatValue` is in percentage points.

> Note: `game.PlayerSustenanceModifier` has no Geography today. Adding a nullable Geography to it
> would let sustenance modifiers target e.g. Hilly Forest; that would be a change to
> [tiles.md](tiles.md).

## Instances

A MapResource on a tile is an **instance** of its MapResourceType. It stores an absolute
`Quantity`, which is **Player-neutral**: every Player extracts from the same amount. (Unlike
sustenance, which stores `Taken` because each Player's value for it differs.)

Amounts are in thousandths, as elsewhere on the campaign map.

## Renewable

### Growth

At the end of every round, each renewable instance with a `Quantity` above `0` grows:

```
Quantity = MIN(MaximumQuantity, Quantity + MAX(MINIMUM_GROWTH, Quantity × GrowthPercentage))
```

- Growth is a share of the **current** quantity, so a small patch grows slowly and a large one
  quickly.
- `MINIMUM_GROWTH` is a static amount, the same for every MapResourceType, so a small quantity does
  not stall on rounding.
- An instance at `0` does not grow. See [Exhaustion and survival](#exhaustion-and-survival).

### Spread

An instance at its `MaximumQuantity` with a `SpreadLimit` above `0` rolls its `SpreadLikelihood`
each round (e.g. Berry 10%). If it spreads, one of the **8 adjacent tiles** is picked at random.
Depending on that tile:

| Target tile | Result | Uses up a spread |
| --- | --- | --- |
| Non-compatible (no row for its Biome/Geography, void, or off an unwrapped map edge) | No spread | No |
| Has a MapResource of another type | No spread | No |
| Has the same MapResource, at its ceiling | No spread | No |
| Has the same MapResource, below its ceiling | Grows by its own `GrowthPercentage` again (it may grow more than once a round: once for itself, and again for each neighbour that spreads into it) | Yes |
| Has the same MapResource, exhausted (dormant) | Revived, as for a new instance | Yes |
| Has no MapResource | A new instance is created | Yes |

A **new or revived** instance starts at `GrowthPercentage × Quantity` of the spreading instance
(capped at the target tile's `MaximumQuantity`).

Picking from all 8 directions, including blocked ones, means a patch with fewer compatible neighbours
(e.g. on a coast) spreads less often. This is intended.

### SpreadLimit

Each instance has a `SpreadLimit` (int), to stop spread going on forever.

- An instance created at map generation gets a random `SpreadLimit` from its MapResourceType's range.
- Each spread that **uses up a spread** (see the table above) decrements the spreading instance's
  `SpreadLimit`.
- A new or revived instance gets the spreading instance's `SpreadLimit` **after** the decrement, so
  each generation can spread less than the one before.

### Exhaustion and survival

An exhausted renewable instance (`Quantity` of `0`) is not removed straight away. It becomes
**dormant**, and each round it has a chance to come back, until it is gone for good:

| Value (per MapResourceType) | Meaning |
| --- | --- |
| `SurvivalRounds` (int) | How many rounds a dormant instance survives, e.g. 5. |
| `ReseedLikelihood` (decimal) | The chance each dormant round that it comes back. |

- If a roll succeeds, the instance comes back at `GrowthPercentage × MaximumQuantity` and grows as
  normal.
- If it fails for `SurvivalRounds` rounds, the instance is **deleted permanently**.
- A dormant instance still takes one of the tile's 2 places and blocks other MapResources spreading
  in. A neighbour of the same type spreading into it revives it (see [Spread](#spread)).

### End of round order

1. **Growth** for every renewable instance with a `Quantity` above `0`.
2. **Survival** rolls for every dormant instance.
3. **Spread**: which instances spread, and in which direction, is decided from the state after
   growth. The spreads are then applied one by one in a fixed order, so two spreads into the same
   empty tile become one new instance and one top-up. The result does not depend on the order tiles
   happen to be read in.

## Distribution

A Distribution holds **several Resources in set amounts**, from one of a set of **scenarios** for its
MapResourceType. For example, Forest Wood (Resources: oak, generic, cedar, ash, ebony, bamboo):

| Scenario | Contents |
| --- | --- |
| 1 | oak 200, generic 10,000, cedar 30 |
| 2 | generic 5,000, ebony 300 |

- A tile's scenario is picked at map generation, using the game seed, weighted by Biome.
- The scenario is a **template** of starting amounts. The instance keeps its **own copy** of each
  amount, so felling oak on tile (4,5) does not change any other tile that uses the same scenario.
- A Distribution never spreads.

### Growth

`MaximumQuantity` comes from the MapResourceType, Biome and Geography table like any other, and caps
the **total** of all the Distribution's Resources. It is not the scenario's total: scenario 1 above
totals 10,230, but its `MaximumQuantity` might be 20,000.

Each round the total grows by `MAX(MINIMUM_GROWTH, Total × GrowthPercentage)`, capped at
`MaximumQuantity`, and the growth is shared between the Resources **in proportion to their current
amounts**. So each Resource expands in ratio until the total hits the ceiling. Scenario 1 at 20,000
would hold roughly oak 391, generic 19,550, cedar 59.

A Resource at `0` gets no share, so once a wood type is gone from a tile it does not come back.

### Extraction

Extraction takes from every Resource **in proportion to their current amounts**, and each amount
taken is **floored to a whole number** (no fractions; what the floor drops is left on the tile).

Taking 200 from scenario 1 (oak 200, generic 10,000, cedar 30, total 10,230):

| | Oak | Generic | Cedar | Total |
| --- | --- | --- | --- | --- |
| Share of 200 | 3.910 | 195.503 | 0.587 | 200 |
| Taken (floored) | 3 | 195 | 0 | 198 |

- Felling gives the specific Resources (oak, generic, cedar) in the amounts taken.
- When a Resource reaches `0`, no more of it is taken from that tile.
- A rare Resource is only taken when its share reaches 1: cedar above needs 341 taken in one go.
  Small extractions from a tile get only its common woods.

> When all of a tile's wood has been extracted, its biome changes. That is a separate task.

The generic wood in a biome's material values ([tiles.md](tiles.md)) is separate from Distributions
and is unchanged.

## Stratified

A stratified MapResource (e.g. a coal seam) is **not renewable**. It is held in **layers**, each of
which may be empty:

| Layer | Contents |
| --- | --- |
| 1 | none, or x of low-quality coal |
| 2 | none, or y of low-quality coal |
| 3 | none, or z of high-quality coal |

- A layer may hold a **different Resource** from the layer above it (e.g. low-grade coal above
  anthracite).
- Layers are extracted **in order**: a layer must be exhausted before the next can be reached.
- Each layer below the first has three gates:
  - its own **discovery likelihood**,
  - a **tech** requirement to survey it (early in the game a Player cannot survey past layer 1), and
  - a **mine level** requirement to reach it.
- A survey can reveal that a layer is empty.

## Discovery

Many MapResources are not automatically visible and must be **discovered**.

- Each MapResourceType has a **discovery likelihood**. Some (e.g. special woods) are found the
  instant a unit enters the tile (likelihood 100%); others (e.g. copper) may be 10%; some locations
  make a MapResource even harder to find than normal.
- Discovery of a MapResourceType can be **switched on and off** per Player by game mechanics. While
  it is off, that Player's discovery likelihood for it is `0`, however their units search. For
  example, uranium is placed at map generation, but no Player can discover it until they reach the
  tech that switches it on, late in the game. Switching it off only stops new discoveries: instances
  a Player has already discovered stay discovered.
- A unit moving into or resting on a tile gets that % chance. A unit **fortifying**, or a nearby
  **settlement**, gives an even higher chance.
- First-time discovery of some MapResources (e.g. metals) adds a **new technology-tree item** that
  must be researched before the empire can use the material.
- When a Player first sees a MapResourceType, a "Research X" item is added to their tech list.
  Researching it makes the Player far more likely to discover other instances of that **specific**
  MapResourceType. For a MapResourceType that requires research before extraction, the Player
  **cannot extract** it until this is researched (e.g. no berries before Berry research).
- An instance created by **spread** is automatically discovered by every Player who had discovered
  the instance it spread from, but it is only **seen** while within one of their Observers' vision.

### Estimated quantity

Players do not see the true `Quantity`. They are shown a **single number**:

```
Shown = Quantity × (1 + EstimationFuzzyFactor × Error)
```

- **`EstimationFuzzyFactor` is per instance.** Each instance gets its own, rolled from its
  MapResourceType's range when it is created (at map generation, or by spread), so one instance can
  be harder to judge than another of the same type.
- **Tech reduces it per Player.** A Player's modifiers for the MapResourceType lower the instance's
  factor for that Player only, so better-equipped Players see closer to the truth.
- **`Error` is between `−1` and `1`, fixed per game seed, Player and instance.** It is **not** rolled
  again on each view, or a Player could average repeated views to find the true amount. As tech
  lowers the factor, the same error shrinks towards the truth.
- **Stratified: per layer.** Each layer of a stratified instance has its own
  `EstimationFuzzyFactor` and `Error` (fixed per game seed, Player, instance and layer), so a deeper
  layer can be harder to judge than the one above it, and layers can be wrong in different
  directions. Each layer's range can be set separately on the MapResourceType.

## Switches

Everything above that is "switched on and off per Player by game mechanics" (discovering a
MapResourceType, receiving one Resource from its yield, surveying one of its layers) is held as
switch rows: one per Player, source, and thing switched, each turning it on or off. They resolve in
this order:

1. Any **off** row wins. A mechanic that forbids something (a taboo, a treaty) overrides the tech
   that allowed it, for as long as the Player holds it.
2. Otherwise any **on** row turns it on.
3. Otherwise the default applies: the MapResourceType's default for discovery, the yielded
   Resource's default, or, for a layer, on for layer 1 and off for every deeper layer (standing in
   for the survey tech until Technology is modelled).

As with modifiers, losing a source just deletes its rows.

## Randomness

Every roll (spread, spread direction, survival, capture, discovery, `SpreadLimit`, estimation error)
is derived from the game's seed, `GameIndex.GenerationSeed`, so the results can be reproduced and
tested.

## Data model

The tables behind this design (CM-47). Amounts are `bigint` thousandths; likelihoods and percentages
are fractions (`0.25` = 25%).

**Catalogue (`map` schema)**, edited in the map editor and read live by every game:

| Table | Holds |
| --- | --- |
| `MapResourceCategory` | The editor's groups. |
| `MapResourceType` | A type's flags, extraction method, discovery values, fuzzy factor range, survival values, and `IconPath` (the image drawn on the tile; null for no icon). |
| `MapResourceTypeYield` | What one extracted unit gives, per Resource, and whether each Resource is on by default. |
| `MapResourceHabitat` | The per Type, Biome and Geography values in [Placement on tiles](#placement-on-tiles). |
| `MapResourceDistributionScenario`, `...Amount`, `...BiomeWeight` | A Distribution's scenarios, their amounts, and their weight in each Biome. |
| `MapResourceTypeLayer` | A stratified type's layers: Resource, chance of being empty, quantity range, discovery likelihood, mine level, fuzzy factor range. |

**Map editor (`mapgallery` schema):** `CampaignMapGalleryResource`, a placement on a gallery tile,
with an optional starting quantity and optional pinned scenario.

**Per game (`game` schema):**

| Table | Holds |
| --- | --- |
| `MapResourceInstance` | An instance on a tile: its slot (1 or 2), `Quantity`, `SpreadLimit`, the Round it went dormant, and its `EstimationFuzzyFactor`. |
| `MapResourceInstanceAmount` | A Distribution instance's own amount of each Resource. |
| `MapResourceInstanceLayer` | A stratified instance's layers: Resource (null when empty), quantity, fuzzy factor. |
| `PlayerMapResourceTypeKnowledge` | The Round a Player first saw a MapResourceType, and the Round they researched it. |
| `PlayerKnownMapResource` | The instances a Player has discovered, and the deepest layer they have surveyed. |
| `PlayerMapResourceModifier` | [Player modifiers](#player-modifiers). |
| `PlayerMapResourceSwitch` | [Switches](#switches). |

The database enforces the limits a defect could otherwise break: at most 2 MapResources on a tile
(two slots, one row each) and one of each type, never renewable and stratified at once, and no
negative quantity. No `Error` is stored for estimation: it is derived from the seed each time it is
needed, so it cannot drift.
