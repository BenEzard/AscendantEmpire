# Production and Assets

## Assets

Everything which is either collectable or manufacturable during the game (**excluding Units**) is
an **Asset**. The majority of assets have a **Material Class**.

When purchasing (e.g. buildings) or manufacturing (e.g. swords), the player expends either:

- a **specific Asset**, or
- a **Material Class**

depending on the requirements of the item.

## Material Classes and Material Preferences

A **Material Class** is a generic category that concrete assets belong to. When an item's cost is
expressed as a Material Class, the engine consults the player's **preference order** for that class
to decide which concrete asset to spend.

Key rule: **the preference order is returned regardless of which assets are currently available.**
It only expresses *preference* — availability is applied afterwards. Assets not yet known to the
empire (undiscovered) are excluded from the returned order.

The player may re-order preferences for any Material Class at any time, changing how assets get
consumed.

### Example 1 — Stone (a map-extracted Asset)

Generic stone, marble, and granite all belong to the Material Class `CONSTRUCTION_STONE`. A default
preference order might be:

| Material Class | Asset | Preference |
| --- | --- | --- |
| CONSTRUCTION_STONE | Generic Stone | 1 |
| | Undiscovered Stone 1 | 2 |
| | Marble | 3 |
| | Granite | 4 |
| | Undiscovered Stone 2 | 5 |

Costs can be specified either as a Material Class or as a specific asset:

| Building | Quantity | Material Class | Asset |
| --- | --- | --- | --- |
| House | 10 | CONSTRUCTION_STONE | |
| Embassy | 100 | | Marble |

Building a **House** costs `10 CONSTRUCTION_STONE`. Because it is a Material Class, any stone works;
the preference returns generic stone first, then marble, then granite (the two undiscovered stones
are excluded). If the player instead had a great supply of marble nearby, they could promote it:

| Material Class | Asset | Preference |
| --- | --- | --- |
| CONSTRUCTION_STONE | Marble | 1 |
| | Undiscovered Stone 1 | 2 |
| | Generic Stone | 3 |
| | Granite | 4 |
| | Undiscovered Stone 2 | 5 |

Now the order is marble, generic stone, granite. The house is built with marble (if available),
which gives **+1 culture per turn**. If no marble is available, preference #2 is used, and so on.

The **Embassy** above is specified as the concrete asset *Marble* — so it must be built with marble
specifically, ignoring the Material Class preference.

### Example 2 — Swords (production)

A swordsman unit design has a `SWORD_MELEE` Material Class assigned to a slot. When a swordsman unit
is created, the material preferences are checked and the **highest available** option is chosen:

| Material Class | Asset | Preference |
| --- | --- | --- |
| SWORD_MELEE | Broad Sword | 1 |
| | Sword (+Sharpened) | 2 |
| | Sword | 3 |
| | Short Sword | 4 |

## Luxury items

The value of an asset is partly determined by how ubiquitous it is. For example, **wine** is a
luxury good — but once an empire has a huge surplus of wine, it is no longer treated as a luxury.

Some assets are **intrinsically** luxury items (gold, silver) and always retain a certain aura of
luxury regardless of surplus.

## Where Assets come from

Most Assets are extracted from the **campaign map**, from the MapResources on its tiles: a berry
patch yields `forage`, a deer yields `medium_game` and a `pelt`. What can sit on a tile, and how it
grows, depletes and is discovered, is described in
[campaign-map/map-resources.md](campaign-map/map-resources.md); how a tile's own biome yields are
depleted and renewed is in [campaign-map/](campaign-map/README.md). How extracted Assets are refined
into finished goods is described in [manufacturing-chains.md](manufacturing-chains.md).

Not every Asset comes off the map. Citizens, slaves and prisoners of war are Assets held in an Asset
Bank and spent like any other, but no MapResource yields them — they come from population growth and
from conquest. An Asset obtainable from the map is simply one that some MapResourceType lists in its
yield; nothing on the Asset itself records this.

The catalogue behind all of this — the meta-categories, categories, groups and Assets themselves —
is described in [assets-data-model.md](assets-data-model.md).
