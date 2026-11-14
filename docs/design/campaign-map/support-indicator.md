# Support indicator

Part of the [Campaign Map](README.md) design.

A tile's **support** says how many turns it can feed a set of Forces from the food available to the
Player: Forage and Hunt on land (Fish as well on a riverbank) or Fish on water, less what has been
taken, together with any MapResource patch standing on it — the same sources `Gathering` feeds from
(`TileFoodSources`). The need is each Force's individuals, healthy and injured alike, times the
quantity its design's body states, counting only Forces that feed (individuals left, not in a Battle,
and a design that states a food need).

Everything is measured in **rations** rather than amounts, because the Assets a tile offers are priced
apart: a berry patch holding a thousand feeds fewer than a thousand of game would. Where the Forces on
a tile eat from different food sets — which no seeded design does, there being one — the tile is
forecast against the set the largest part of the need belongs to.

The turns are **forecast** one turn at a time (`TileSupportForecaster`), on copies of the tile's
depletion row and its patches, with the same rules the turn pipeline uses:

1. The Forces feed: the need is split across the sources as `Gathering` splits it. A turn only counts
   when they cover the whole need.
2. The round ends: the biome regrows as in [Replenishment](replenishment.md), as fed on. A patch does
   not — MapResource growth is not modelled, so a patch eaten is a patch gone.

A Player's feed always comes before the round's regrowth, so this order holds whatever the turn
order. The forecast stops after **5 turns**; a tile that still feeds the Forces after that lasts
beyond the forecast (possibly for good, when the regrowth covers the need). A biome with no
Regeneration Percent does not regrow, so its turns are plain available ÷ need.

| Level | Rule | Colour |
| --- | --- | --- |
| Exhausted | Nothing available at all now, from the biome or from a patch. Shown even with no Forces on the tile. | Dark red |
| *(none)* | Food available but no need. | No colour |
| Plenty | Feeds the Forces for 2 turns or more. | Green |
| OneTurn | Feeds them for 1 turn. | Yellow |
| Insufficient | Some food, but not enough for a whole turn. | Red |

The rules are checked in that order. A stripped tile is Exhausted even though it will regrow: the
Forces on it go hungry this turn. A tile whose biome is stripped but which still carries a full patch
is not Exhausted — the Forces on it eat perfectly well.

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
  the tile. It is returned with the tile's sustenance, alongside the figure described below.
- **Hover marker.** While Forces are selected, a circle at the centre of the hovered tile, holding the
  number of turns: `0` to `5`, or `5+` beyond the forecast. The need is the selected Forces plus the
  Player's own Forces already on the hovered tile, each counted once. No marker is drawn on the
  selected Forces' own tile. Selecting Forces makes one request,
  `GET campaign-map/player/tiles/support`, which returns the level of every tile in live sight, so
  hovering is a lookup rather than a request per tile.

Both only disclose tiles in the Player's live sight, like the sustenance values: a level reveals what
is left on a tile, which changes as anyone feeds on it.

## What the toolbar's figure means

The apple on the tile toolbar is **how many individuals the tile feeds for one Turn** — the same
rations the level is forecast in, rather than a sum of amounts, because the Assets a tile offers are
priced apart and adding them would add unlike things. Hovering it breaks the figure into its sources
in the same unit, so the parts add up: "Forage (400) + Hunt (12) + Berries (~359)".

- It is priced against the food set the Forces **standing on the tile** eat from, and where none
  stand there, the set the Player's Forces elsewhere eat from. With one Food set seeded these are the
  same set. A Player with no Force left that eats is shown no figure at all: the tile's food is real,
  but there is nothing to measure it in.
- A **MapResource patch counts towards it**, but only one the Player has **discovered**, and then at
  their own estimate of its size. The feed itself ignores discovery — a Force camped on berries eats
  them whether or not anyone surveyed them — but a figure that counted an undiscovered patch would
  tell the Player food is there before they find it.
- Any number drawn from an estimate carries a **tilde**: the figure under a patch's icon, the patch's
  part of the breakdown, and the total itself as soon as a patch is any part of it. The Player's own
  biome values are not estimates and carry none. The tilde is for the eye; a screen reader is told
  "about" in words.

The level above is worked out from the **true** quantities, so a tile whose patch the Player
overestimates can be coloured worse than its total reads. That is the estimate showing, which is the
point of fuzzing it, and not a disagreement to fix.
