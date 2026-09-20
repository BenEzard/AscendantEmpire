# Unit Support and Supply

Units have ongoing **support** needs. There are four types: **fuel**, **ammunition**, **food**, and
**medical supplies**. Within an empire's own territory (or an ally's, under a Support Agreement)
supply is implicit; beyond it, a **supply chain** is required.

General notes:

- Some specialists (e.g. a fuel tanker) can supply units for a single turn when out of supply.
- Some terrain improvements (e.g. a concealed fuel/ammunition dump) can supply.
- When a unit lacks its requirements, this must be shown clearly on the UI.
- An intelligent, simple way is needed to decide which Units in a Command miss out on fuel and
  ammunition. (Perhaps the player selects which units are resupplied in the event of a battle.)

## Supply chains

A **supply chain** is the logistics link back to allied supplies for a unit operating outside
friendly territory.

- A supply chain is **implicit** (not needed) inside the empire's own territory, or an ally's where
  a Support Agreement exists. (Resources are still deducted as support — there is just no chaining
  required.)
- An **intact** supply chain exists where units carrying the SUPPLY component are daisy-chained back
  to friendly territory. The chain is **broken** when it is not contiguous; any units beyond the
  break (including supply units) are unsupplied.
- The supply radius of chained units must **overlap or touch** (be adjacent) for the chain to be
  active.

## Fuel

- Fuel types: fodder (mounted units), steam (wood), coal, oil, diesel, marine diesel, petrol,
  airplane fuel, nuclear.
- How fuel limits movement is in unit-movement.md, for [the campaign map](unit-movement.md#fuel) and
  [battle](unit-movement.md#fuel-1).

## Ammunition

- A weapon component requires **0 or 1** ammunition type to attack and defend.
- Ammunition types: sling shot, arrows, bullets (generic), artillery rounds, high-energy rounds,
  piercing rounds, rockets, etc.
- A unit **out of ammunition** attacks and defends at **30%** of its normal values.
- A unit may carry multiple weapons and uses the most effective weapon for which it has ammunition.

## Food

- A unit base component requires **1** food type to remain healthy.
- Food types: fodder (mounted units), food (men), ration packs (men).
- A unit that goes short of food takes **casualties in proportion to its shortfall**. At total
  starvation it loses **34%** of its troops in one turn; going half short costs half of that. The
  total is rounded **down**, so a large unit carrying a small deficit loses nobody.
- A single roll splits that total between **dead** and **wounded**: between 10% and 30% of the
  casualties die and the rest are wounded, rounded in the wounded's favour. How many suffer is
  fixed by the shortfall; only who dies is left to chance.
- Deaths are taken from the **already wounded** first and reach the healthy only once the wounded
  are gone. Wounds are taken from the healthy — and a wound with no healthy man left to take it
  **kills instead**. A unit left hungry therefore accumulates wounded and then begins losing men.
- Each Force takes its own casualties; a shortfall shared by several units on one tile is split
  between them in proportion to their troops before any of this is applied.
- If a tile holds **≤ 15 individuals** in total, the units on it take wounds but never deaths. The
  gate is per tile rather than per unit, so splitting an army into small bands does not buy it.
- With the **Mobile Scavenging** technology, a unit takes a percentage of a tile's food as it moves
  *through* it (i.e. it neither starts nor ends its turn there).
- A unit consumes food from the tile in which it **ends** its turn. If that tile doesn't produce
  enough on its own, the excess must come via a supply chain. Tile food replenishes at a set rate;
  if a unit consumes more than remains, it must move or starve.

**Food algorithm (per turn):**

1. If the unit moves through a tile with remaining movement points, take *x%* of the tile's food.
2. On the tile where the unit can move no further, calculate the food required for the units there.
3. Deduct the food available on the tile. Is more food still required?
   - **No** → end.
   - **Yes** → is this territory owned, with a Support Agreement in place with the host empire?
     - **Yes** → deduct food from the asset banks of the appropriate empires.

See [campaign-map/depletion.md](../campaign-map/depletion.md) for the full tile depletion and
regeneration mechanics.

## Medical supplies

- When a unit contains wounded, medical supplies aid quicker restoration of health.
- Without medical supplies, casualty rates from battles are higher and restoration is much slower.
