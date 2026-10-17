# Support indicator

Part of the [Campaign Map](README.md) design.

A tile's **support** says how many turns it can feed a set of Forces from the food available to the
Player: the pool of Forage and Hunt on land, or Fish on water, less what has been taken — the same
pool `Gathering` feeds from (`TileFoodPool`). The need is 1 food per troop, healthy and injured,
counting only Forces that feed (troops left, not in a Battle).

The turns are **forecast** one turn at a time (`TileSupportForecaster`), on a copy of the tile's
depletion, with the same rules the turn pipeline uses:

1. The Forces feed: the need is split across the pool as `Gathering` splits it. A turn only counts
   when the pool covers the whole need.
2. The round ends: the tile regrows as in [Replenishment](replenishment.md), as fed on.

A Player's feed always comes before the round's regrowth, so this order holds whatever the turn
order. The forecast stops after **5 turns**; a tile that still feeds the Forces after that lasts
beyond the forecast (possibly for good, when the regrowth covers the need). A biome with no
Regeneration Percent does not regrow, so its turns are plain available ÷ need.

| Level | Rule | Colour |
| --- | --- | --- |
| Exhausted | Nothing available at all now. Shown even with no Forces on the tile. | Dark red |
| *(none)* | Food available but no need. | No colour |
| Plenty | Feeds the Forces for 2 turns or more. | Green |
| OneTurn | Feeds them for 1 turn. | Yellow |
| Insufficient | Some food, but not enough for a whole turn. | Red |

The rules are checked in that order. A stripped tile is Exhausted even though it will regrow: the
Forces on it go hungry this turn.

The forecast answers **how long these Forces can stay**. It holds their size, the Player's modifiers
and the biome fixed, and assumes nobody else feeds on the tile. That holds while the Forces stand on
it: with no alliance model, any other Player stepping on starts a Battle, and Forces in a Battle do
not feed. Before they arrive a rival can still feed there; the forecast is refetched after every step.

> TODO: The 20% pass-through draw will let Forces moving through an occupied tile eat from food the
> forecast treats as the Forces' own, and alliances (COMM-4) will let another Player's Forces share
> a tile. The forecast needs revisiting when either lands.
>
> TODO: An owned tile regrows against its owner's effective values, so forecasting on a rival's land
> reveals how much their tech boosts food. A non-owner is shown those values, and the leak is
> deliberate: an occupier collects the tile's yield at the owner's levels anyway, so standing on
> enemy farmland is meant to tell you how good it is. See
> [tile-ownership.md](tile-ownership.md#occupation). Waits on Settlements.
>
> TODO: A hidden Force's feeding must not reveal it — see
> [Hidden Forces and food](vision.md#hidden-forces-and-food).

The level is shown in two places:

- **Tile toolbar.** The selected tile's background. The need is the Player's own Forces standing on
  the tile. It is returned with the tile's sustenance.
- **Hover marker.** While Forces are selected, a circle at the centre of the hovered tile, holding the
  number of turns: `0` to `5`, or `5+` beyond the forecast. The need is the selected Forces plus the
  Player's own Forces already on the hovered tile, each counted once. No marker is drawn on the
  selected Forces' own tile. Selecting Forces makes one request,
  `GET campaign-map/player/tiles/support`, which returns the level of every tile in live sight, so
  hovering is a lookup rather than a request per tile.

Both only disclose tiles in the Player's live sight, like the sustenance values: a level reveals what
is left on a tile, which changes as anyone feeds on it.
