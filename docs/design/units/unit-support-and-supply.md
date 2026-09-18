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
- A unit **out of food** injures **34%** of its initial force strength per turn. When the unit is
  100% wounded, 34% (or the remainder) dies. Weakening is spread evenly across all units in a
  Command.
- If a Unit Stack has **≤ 15 individuals** it is "free" from food requirements (the calculation is
  skipped).
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
