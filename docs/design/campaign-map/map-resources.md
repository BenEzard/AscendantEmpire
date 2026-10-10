# MapResources

Part of the [Campaign Map](README.md) design.

A **MapResource** is something on a map tile that can be extracted: a berry patch, a herd of deer, a
coal seam, a forest's mix of woods. When extracted, it gives up **one or more Assets** (e.g. a
Berry gives `forage`; a Deer gives `medium_game` and `pelt`). Extracted Assets are held in an Asset
Bank (see [production-and-assets.md](../production-and-assets.md) and
[assets-data-model.md](../../assets-data-model.md)).

> **Naming.** Always write **MapResource** (and `MapResourceType`) for the thing on the tile, never
> "Asset". An Asset is what extracting one *yields*: the two sit either side of extraction, and a
> MapResourceType can yield several Assets at once.

MapResources appear on a `CampaignMapTile`.

> **Later: battle maps.** MapResources may also appear on battle maps (e.g. a berry patch or a stand
> of ebony on specific battle tiles). If we do this, battle map tiles will need a lot more work: they
> would have to carry their own MapResources, stay consistent with the campaign tile's MapResources,
> and interact with the battle map TERRAIN actions (clear forest, despoil forest) in
> [battlefield-maps.md](../battlefield-maps.md).

## MapResourceTypes

A `MapResourceType` (e.g. Berry, Deer, Horse, Coal Seam, Timber) defines how its instances
behave. Its kinds are set by flags:

| Flag | Meaning |
| --- | --- |
| `Renewable` (bool) | Grows every round, and may spread. See [Renewable](#renewable). |
| `Stratified` (bool) | Not renewable. Held in layers, reached in order. See [Stratified](#stratified). |
| `Distribution` (bool) | Renewable, holds several Assets in set amounts, never spreads. See [Distribution](#distribution). |

- `Renewable` and `Stratified` are **mutually exclusive**, enforced by the MapResourceType validator.
- A `Distribution` must also be `Renewable`, and must not spread (no `SpreadLikelihood`).
- **Icon.** Most MapResourceTypes show an icon on the tile. Some, such as a Distribution of wood in a
  forest, have none: the biome already says "forest".
- **Yield.** Every MapResourceType gives up **one or more Assets**. It lists the Assets, and
  how much of each, that one unit extracted gives: a Berry gives one (`forage`); a Deer gives two
  (`medium_game` and `pelt`). A Distribution is different: it holds a separate amount of each of its
  Assets, and extracting it gives some of each (see [Distribution](#distribution)).
- **Extraction method.** Each MapResourceType has one: e.g. Gather, Hunt, Fell, Mine, or **Capture**
  (horses), which succeeds with a `CaptureLikelihood`. Extracting always reduces the quantity on the
  tile.
- **Research first (some MapResourceTypes).** A MapResourceType may require research before it can
  be extracted (e.g. Berry research before anyone in the empire can take berries). Others can be
  extracted as soon as they are discovered. See [Discovery](#discovery).
- **Each yielded Asset can be switched on and off.** Each Asset in a MapResourceType's yield
  can be switched on and off per Player by game mechanics (e.g. a tech). Extracting gives only the
  Assets that are switched on for that Player; the quantity on the tile still goes down by the
  full amount extracted. For example, once Deer is researched a Deer gives `medium_game` straight
  away, but gives `pelt` only after the Player unlocks the hide-working tech. Until then, the hides
  of deer taken are lost. This does not apply to a Distribution: all of its Assets are always
  given.

> Extraction itself is out of scope here, apart from the rules above and the Distribution rules
> below. In a Player's controlled area it is done by improvements
> (or from a nearby camp or Settlement, e.g. berries, without an improvement on the tile). In
> unclaimed or enemy land, a Force feeds from a food MapResource such as a berry patch: it is the
> `[Additional Assets]` term in the total-asset formula in [tiles.md](tiles.md).

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

To place one, choose it from the MapResources palette's dropdown and click a tile. It takes the
tile's first free slot, and a dialog opens to set its starting quantity or pin a Distribution
scenario (either left empty is decided per game), or to delete it. A tile is refused when the type
has no habitat for its Biome and Geography, when it already holds that type, or when both slots are
taken. Clicking a tile that already holds the chosen type opens its dialog instead.

The categories and types themselves are edited in the editor's **MapResource Type Manager** (the
MapResources palette button). A type's and a category's code is its fixed key once saved. Deleting a
type is refused while any map places it or any game uses it, and deleting a category is refused while
it has types. The database seed creates the categories only: a type's Assets must exist first.

**Timber is pre-assigned.** Generating a map gives every Forest, Jungle and Woodland tile a Timber
placement, in the tile's first free slot, with no quantity and no pinned scenario. The editor's Biome
brush keeps that in step: painting a tile one of those Biomes adds the placement, and painting it out of
Timber's habitat removes it again — unless the author pinned a scenario or set a quantity on it, in which
case the tile's Biome is left unchanged and the editor is told which tiles held out. A tile whose two
slots are already taken by other MapResources gains no Timber. Which Biomes count is read from Timber's
habitats rather than named in code, so a habitat added in the MapResource Type Manager takes effect with
no code change.

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

- An instance created at game creation gets a random `SpreadLimit` from its habitat's range.
- Each spread that **uses up a spread** (see the table above) decrements the spreading instance's
  `SpreadLimit`.
- A new or revived instance gets the spreading instance's `SpreadLimit` **after** the decrement, so
  each generation can spread less than the one before.

### Exhaustion and survival

An exhausted renewable instance (`Quantity` of `0`) is not removed straight away. It becomes
**dormant**, and each round it has a chance to come back, until it is gone for good:

| Value (per MapResourceType) | Meaning |
| --- | --- |
| `SurvivalRounds` (int) | How many rounds a dormant instance survives, e.g. 5. `0` is deleted on exhaustion, never dormant. |
| `ReseedLikelihood` (decimal) | The chance each dormant round that it comes back. |

- If a roll succeeds, the instance comes back at `GrowthPercentage × MaximumQuantity` and grows as
  normal.
- If it fails for `SurvivalRounds` rounds, the instance is **deleted permanently**.
- At `SurvivalRounds` of `0` the instance is deleted the moment it is exhausted. It has no dormant
  round, so it is never rolled and `ReseedLikelihood` never applies — though a renewable
  MapResourceType still carries one, since the two are set together whatever the round count.
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

A Distribution holds **several Assets in set amounts**, from one of a set of **scenarios** for its
MapResourceType. For example, Timber (Assets: oak, generic, cedar, ash, ebony, bamboo):

| Scenario | Contents |
| --- | --- |
| 1 | oak 200, generic 10,000, cedar 30 |
| 2 | generic 5,000, ebony 300 |

- A Forest, Jungle or Woodland tile is given its Timber placement at **map generation**, and in the map
  editor when a tile is painted one of those Biomes. The placement says only that there is wood there.
- Which scenario that wood turns out to be is picked at **game creation**, from
  `GameIndex.GenerationSeed`, weighted by Biome — so two games from the same map get different mixes on
  the same tile.
- The scenario is a **template** of starting amounts. The instance keeps its **own copy** of each
  amount, so felling oak on tile (4,5) does not change any other tile that uses the same scenario.
- A Distribution never spreads.

### Growth

`MaximumQuantity` comes from the MapResourceType, Biome and Geography table like any other, and caps
the **total** of all the Distribution's Assets. It is not the scenario's total: scenario 1 above
totals 10,230, but its `MaximumQuantity` might be 20,000.

Each round the total grows by `MAX(MINIMUM_GROWTH, Total × GrowthPercentage)`, capped at
`MaximumQuantity`, and the growth is shared between the Assets **in proportion to their current
amounts**. So each Asset expands in ratio until the total hits the ceiling. Scenario 1 at 20,000
would hold roughly oak 391, generic 19,550, cedar 59.

An Asset at `0` gets no share, so once a wood type is gone from a tile it does not come back.

### Extraction

Extraction takes from every Asset **in proportion to their current amounts**, and each amount
taken is **floored to a whole number** (no fractions; what the floor drops is left on the tile).

Taking 200 from scenario 1 (oak 200, generic 10,000, cedar 30, total 10,230):

| | Oak | Generic | Cedar | Total |
| --- | --- | --- | --- | --- |
| Share of 200 | 3.910 | 195.503 | 0.587 | 200 |
| Taken (floored) | 3 | 195 | 0 | 198 |

- Felling gives the specific Assets (oak, generic, cedar) in the amounts taken.
- When an Asset reaches `0`, no more of it is taken from that tile.
- A rare Asset is only taken when its share reaches 1: cedar above needs 341 taken in one go.
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

- A layer may hold a **different Asset** from the layer above it (e.g. low-grade coal above
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
  MapResourceType's range when it is created (at game creation, or by spread), so one instance can
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
MapResourceType, receiving one Asset from its yield, surveying one of its layers) is held as
switch rows: one per Player, source, and thing switched, each turning it on or off. They resolve in
this order:

1. Any **off** row wins. A mechanic that forbids something (a taboo, a treaty) overrides the tech
   that allowed it, for as long as the Player holds it.
2. Otherwise any **on** row turns it on.
3. Otherwise the default applies: the MapResourceType's default for discovery, the yielded
   Asset's default, or, for a layer, on for layer 1 and off for every deeper layer. The survey tech
   that would turn a deeper layer on is not yet granted by any technology, so the switch stands in
   for it.

As with modifiers, losing a source just deletes its rows.

## Randomness

Every roll (spread, spread direction, survival, capture, discovery, `SpreadLimit`, estimation error)
is derived from the game's seed, `GameIndex.GenerationSeed`, so the results can be reproduced and
tested. No roll's result is stored.

A roll's key is the seed plus whatever tells that roll apart from every other roll drawn from the
same seed. What that takes depends on whether the roll is taken once or taken again:

| Roll | Key | Notes |
| --- | --- | --- |
| Survival (reseed) | seed + instance `Id` + **round** + roll name | Taken once in each dormant round. The round is what makes those rolls differ: keyed on the seed and the instance alone, a dormant instance would draw the same number every round, and so would either reseed on its first dormant round or never reseed at all. |
| Spread, and spread direction | seed + instance `Id` + **round** + roll name | Taken once a round while the instance has spreads left. The direction must vary from round to round as well, so it carries the round too. |
| Discovery | seed + instance `Id` + Player `Id` + **round** + roll name | Taken per Player, per round one of their units enters or rests on the tile, so a round that fails does not fix the outcome for every later round. A stratified type rolls per layer, which the roll name carries. |
| Capture | seed + instance `Id` + Player `Id` + **round** + roll name | Taken per extraction attempt, so a failed capture is not failed identically for good. |
| `SpreadLimit`, initial quantity, Distribution scenario, `EstimationFuzzyFactor` | seed + instance `Id` + roll name | Rolled once, when the instance is created, and fixed for its life — so these carry no round. `EstimationFuzzyFactor` depends on it: it is recomputed on every view rather than stored, which only holds while the key gives the same answer every time it is asked. |

The **roll name** separates two rolls that would otherwise share a key, such as a spread and its
direction in the same round.

## Data model

The tables behind this design (CM-47). Amounts are `bigint` thousandths; likelihoods and percentages
are fractions (`0.25` = 25%).

**Catalogue (`map` schema)**, edited in the map editor and read live by every game:

| Table | Holds |
| --- | --- |
| `MapResourceCategory` | The editor's groups. |
| `MapResourceType` | A type's flags, extraction method, discovery values, fuzzy factor range, survival values, and `IconPath` (the image drawn on the tile; null for no icon). |
| `MapResourceTypeYield` | What one extracted unit gives, per Asset, and whether each Asset is on by default. |
| `MapResourceHabitat` | The per Type, Biome and Geography values in [Placement on tiles](#placement-on-tiles). |
| `MapResourceDistributionScenario`, `...Amount`, `...BiomeWeight` | A Distribution's scenarios, their amounts, and their weight in each Biome. |
| `MapResourceTypeLayer` | A stratified type's layers: Asset, chance of being empty, quantity range, discovery likelihood, mine level, fuzzy factor range. |

**Map editor (`mapgallery` schema):** `CampaignMapGalleryResource`, a placement on a gallery tile,
with an optional starting quantity and optional pinned scenario.

**Per game (`game` schema):**

| Table | Holds |
| --- | --- |
| `MapResourceInstance` | An instance on a tile: its slot (1 or 2), `Quantity`, `SpreadLimit`, the Round it went dormant, and its `EstimationFuzzyFactor`. |
| `MapResourceInstanceAmount` | A Distribution instance's own amount of each Asset. |
| `MapResourceInstanceLayer` | A stratified instance's layers: Asset (null when empty), quantity, fuzzy factor. |
| `PlayerMapResourceTypeKnowledge` | The Round a Player first saw a MapResourceType, and the Round they researched it. |
| `PlayerKnownMapResource` | The instances a Player has discovered, and the deepest layer they have surveyed. |
| `PlayerMapResourceModifier` | [Player modifiers](#player-modifiers). |
| `PlayerMapResourceSwitch` | [Switches](#switches). |

The database enforces the limits a defect could otherwise break: at most 2 MapResources on a tile
(two slots, one row each) and one of each type, never renewable and stratified at once, and no
negative quantity. No `Error` is stored for estimation: it is derived from the seed each time it is
needed, so it cannot drift.

### Explanation of Distribution Data Model

A Distribution spans the most tables of any kind, so here is one worked through: a `Timber` type,
from its catalogue rows to two adjacent tiles that start different and then diverge. Amounts are the
raw stored values, with the logical figure in brackets where it helps.

The six Assets it draws on are `assets.Asset` rows the Asset catalogue already holds:

| Id | DisplayName |
| --- | --- |
| 41 | Oak Log |
| 42 | Generic Log |
| 43 | Cedar Log |
| 44 | Ash Log |
| 45 | Ebony Log |
| 46 | Bamboo |

**`map.MapResourceType`** — one row, in the `wood` category (id 3):

| Column | Value |
| --- | --- |
| Id | 7 |
| Code | `Timber` |
| DisplayName | Timber |
| MapResourceCategoryId | 3 |
| IconPath | `NULL` — the Biome already says "forest" |
| IsRenewable | 1 |
| IsDistribution | 1 |
| IsStratified | 0 |
| ExtractionMethod | Fell |
| CaptureLikelihood | `NULL` |
| RequiresResearchToExtract | 0 |
| IsDiscoverableByDefault | 1 |
| DiscoveryLikelihood | 1.000 — wood is seen on sight |
| EstimationFuzzyFactorMin / Max | 0.050 / 0.150 |
| SurvivalRounds / ReseedLikelihood | 5 / 0.200 |

**`map.MapResourceTypeYield`** has **no rows**. A Distribution holds its Assets in its scenarios, and
the MapResourceType validator rejects yield rows on one.

**`map.MapResourceHabitat`** — where it can exist, and its ceiling there:

| Id | TypeId | BiomeTypeId | GeographyTypeId | InitialQuantityMin/Max | MaximumQuantity | GrowthPercentage | Spread columns |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 55 | 7 | Forest (8) | `NULL` | `NULL` | 20000000 [20,000] | 0.030 | `NULL` |
| 56 | 7 | Forest (8) | Hilly | `NULL` | 14000000 [14,000] | 0.025 | `NULL` |
| 57 | 7 | Jungle (9) | `NULL` | `NULL` | 26000000 [26,000] | 0.040 | `NULL` |
| 58 | 7 | Woodland (10) | `NULL` | `NULL` | 9000000 [9,000] | 0.020 | `NULL` |

Row 56 is a Geography-specific habitat beating the Biome-wide one, so a hilly forest holds less wood.
`InitialQuantityMin` and `InitialQuantityMax` are null because a Distribution starts at its
scenario's amounts, and all three spread columns are null because a Distribution never spreads; the
validator enforces both.

**`map.MapResourceDistributionScenario`** — three of the type's scenarios:

| Id | TypeId | ScenarioNumber | DisplayName |
| --- | --- | --- | --- |
| 301 | 7 | 1 | Mixed oak |
| 302 | 7 | 2 | Ebony stand |
| 303 | 7 | 17 | Bamboo thicket |

**`map.MapResourceDistributionScenarioAmount`** — the templates themselves, keyed on the scenario and
the Asset, so an Asset cannot appear twice in one scenario:

| ScenarioId | AssetId | Amount |
| --- | --- | --- |
| 301 | 41 (Oak) | 200000 [200] |
| 301 | 42 (Generic) | 10000000 [10,000] |
| 301 | 43 (Cedar) | 30000 [30] |
| 302 | 42 (Generic) | 5000000 [5,000] |
| 302 | 45 (Ebony) | 300000 [300] |
| 303 | 46 (Bamboo) | 12000000 [12,000] |
| 303 | 42 (Generic) | 1500000 [1,500] |
| 303 | 44 (Ash) | 40000 [40] |

**`map.MapResourceDistributionScenarioBiomeWeight`** — which Biomes each scenario can appear in, and
how often:

| ScenarioId | BiomeTypeId | Weight |
| --- | --- | --- |
| 301 | Forest (8) | 20 |
| 301 | Woodland (10) | 12 |
| 302 | Forest (8) | 1 |
| 302 | Woodland (10) | 1 |
| 303 | Jungle (9) | 25 |

The **missing** rows carry as much meaning as the present ones. Scenario 301 has no Jungle row and
303 has no Forest or Woodland row, which is what keeps bamboo out of forests and oak out of jungles.
The weights are what make a wood rare: on a Forest tile the roll is over `{301: 20, 302: 1}`, so an
ebony stand lands once in 21 tiles.

**`mapgallery.CampaignMapGalleryResource`** — the placements on the gallery map:

| Id | CampaignMapGalleryId | MapResourceTypeId | Slot | InitialQuantity | ScenarioId |
| --- | --- | --- | --- | --- | --- |
| 9001 | 48812 (Forest, 4/5) | 7 | 1 | `NULL` | `NULL` |
| 9002 | 48813 (Forest, 5/5) | 7 | 1 | `NULL` | `NULL` |
| 9003 | 48813 | 4 (Deer) | 2 | 6000000 | `NULL` |

The two wood placements are identical: the row says only "there is wood here", leaving which mix to
each game. A placement pins a scenario by setting `ScenarioId` instead. Row 9003 is a Deer holding
the same tile's second slot, so tile 48813 is full.

**`game.MapResourceInstance`** — what game creation creates from those placements:

| Id | GameId | CampaignMapId | TypeId | Slot | Quantity | SpreadLimit | DormantSinceRound | EstimationFuzzyFactor | ScenarioId | CreatedOnRound |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 70114 | 93 | 120488 (4/5) | 7 | 1 | `NULL` | `NULL` | `NULL` | 0.082 | 301 | 0 |
| 70115 | 93 | 120489 (5/5) | 7 | 1 | `NULL` | `NULL` | `NULL` | 0.114 | 302 | 0 |

Two adjacent Forest tiles from one placement each, rolled onto **different scenarios** — one mixed
oak, one an ebony stand. `Quantity` is null because the amounts are in child rows, `SpreadLimit` is
null because a Distribution never spreads, and each `EstimationFuzzyFactor` was rolled from the
type's 0.050–0.150 range keyed on the seed and the instance `Id`. Another game from the same map
rolls both again.

**`game.MapResourceInstanceAmount`** — each instance's own copy of its scenario:

| InstanceId | AssetId | GameId | Amount |
| --- | --- | --- | --- |
| 70114 | 41 (Oak) | 93 | 200000 [200] |
| 70114 | 42 (Generic) | 93 | 10000000 [10,000] |
| 70114 | 43 (Cedar) | 93 | 30000 [30] |
| 70115 | 42 (Generic) | 93 | 5000000 [5,000] |
| 70115 | 45 (Ebony) | 93 | 300000 [300] |

#### How two tiles diverge

Felling 200 from instance 70114 takes from each Asset in proportion to its current amount, floored to
a whole unit, against a total of 10,230:

| | Oak | Generic | Cedar |
| --- | --- | --- | --- |
| Share of 200 | 3.910 | 195.503 | 0.587 |
| Taken, floored | 3 | 195 | 0 |
| Amount after | 200000 → 197000 | 10000000 → 9805000 | 30000 (unchanged) |

Cedar is untouched because its share never reached 1: a small felling only yields a tile's common
woods. The end of the Round then grows the new total of 10,032 by the Forest habitat's 0.030, shared
in proportion again and capped at 20,000:

| | Oak | Generic | Cedar |
| --- | --- | --- | --- |
| Share of 300.96 | 5.909 | 294.146 | 0.900 |
| Amount after | 202909 | 10099146 | 30900 |

Storing thousandths is what makes this work: cedar's 0.900 of growth is 900 raw units, so a rare wood
still grows rather than rounding away to nothing each Round. Meanwhile instance 70115 grows its own
generic and ebony on its own track, and the two tiles' compositions drift apart for good — once an
Asset reaches 0 it gets no share of growth and never returns.

## Accepted limitations

Wood is one `Timber` type covering Forest, Jungle and Woodland, rather than a type per Biome or a
type per species. The 2-slot limit is what forces that: six separate wood types could never sit on
one tile, and a forest holding only oak would be wrong. One type also matches what is genuinely
shared across every wood — they are all felled, all seen on sight, and all improved by the same
forestry tech. Three consequences follow, and all three are accepted.

**A Distribution's Assets cannot be tech-gated.** The per-Asset switches that hold back a Deer's
`pelt` until hide-working cover `MapResourceTypeYield` rows only, and a Distribution has none, so
[Switches](#switches) never applies to one and all of its Assets are always given. A Stone Age Player
felling a Timber tile that holds ebony gets the ebony logs immediately. Splitting the exotic woods
into their own types would restore gating, but only by removing them from Timber's scenarios
entirely, which costs the sprinkling of a rare wood through an ordinary forest that the Distribution
kind exists to express. It would also give up the guarantee that `UX_MapResourceInstance_Tile_Type`
provides today, that one tile holds at most one wood MapResource.

**Timber takes one of a forest tile's two slots.** Every Forest, Jungle and Woodland tile that holds
Timber has a single slot left for everything else on it — deer, berries, copper — even though Timber
draws no icon of its own, the Biome having already said "forest".

**Discovery does nothing for Timber.** Its `DiscoveryLikelihood` is 1, so trees are seen on sight and
the loop where first sight adds a "Research X" item that improves later discovery of the same type
has nothing to improve. No research item is generated for a type found on sight.

## Outstanding decisions

| Open question | Where it bites |
| --- | --- |
| Nothing validates a scenario's total against the habitat ceiling. A scenario whose amounts total more than the `MaximumQuantity` of a Biome it is weighted in creates an instance born above its ceiling, and growth only ever adds. The seeded Timber scenarios cannot break it — their generator withholds a Biome weight wherever the total would not fit — but a scenario authored in the MapResource Type Manager can. | `MapResourceTypeValidator` |
| The rounding direction of each Asset's share of growth is unspecified, and the shares rarely sum to exactly the total growth, so the total either creeps towards `MaximumQuantity` without reaching it or overshoots. | [Distribution › Growth](#growth-1) |
| Tech-gating a Distribution's Assets. `PlayerMapResourceSwitch` already carries an `AssetId`, so this needs a switch kind and an extraction rule rather than a schema change — but a per-Asset default has nowhere to live, since a Distribution type's Asset set exists only as the union of its scenarios' amounts. A type-level list of the Assets a Distribution can hold, each with its own default, would give the default a home and let the validator check that every scenario amount names a declared Asset. | [Switches](#switches) |
| The tile icon for a Distribution whose majority Asset, ignoring generic, is one particular wood. It can be derived per view from the instance's amounts and drawn from `assets.Asset.IconPath`, with no new column: `EstimationFuzzyFactor` is per instance and `Error` is per seed, Player and instance, so every Asset is scaled by the same multiplier and the majority a Player sees is the true one. What share counts as a majority is undecided. | [Distribution](#distribution) |
| Whether a Distribution implied by its Biome should take a tile slot at all, or whether the ceiling of 2 should rise. | [Placement on tiles](#placement-on-tiles) |
| Jittering the copied amounts at game creation, by a share keyed on the seed and the instance `Id`, so two tiles drawing the same scenario do not start identical. `MapResourceInstanceAmount` is already an independent copy and the scenario is never read back, so nothing downstream assumes they match. The lever to reach for if a fixed set of scenarios reads too uniform across hundreds of tiles. | [Distribution](#distribution) |
| `SurvivalRounds` and `ReseedLikelihood` are on the MapResourceType, so a stripped jungle and a stripped woodland must recover at the same rate. They are habitat-shaped values, and moving them onto `MapResourceHabitat` alongside `GrowthPercentage` would let dormancy vary by Biome for every renewable type. | [Exhaustion and survival](#exhaustion-and-survival) |
| Nothing checks that a scenario pinned on a gallery placement is weighted in that tile's Biome, so a bamboo scenario can be pinned onto a Forest tile. Habitat compatibility is checked per type, which cannot see it. | `UpdateGalleryMapResourceHandler` |
| A Distribution instance's total is not stored. `MapResourceInstance.Quantity` is null and the total is summed from `MapResourceInstanceAmount` wherever it is needed, including growth. | [Instances](#instances) |
| Importing a map creates its tiles but none of the placements their Biomes imply, so an imported Forest map starts with no Timber until a tile is repainted. Generation and the editor's Biome brush both pre-assign it; import has no equivalent pass, and the export document carries no placements to restore either. | `MapImportExecutor` |
| Only Timber is seeded. The seed's **Map Resources** target writes every resource registered in `MapResourceTypeSeedData`, and Timber — its habitats and its 45 generated scenarios — is the only one so far; Berry, Deer, Coal Seam and the rest are still authored by hand in the MapResource Type Manager. Adding one is a seed-data file plus a line of that registry. | `MapResourceTypeSeedData` |
