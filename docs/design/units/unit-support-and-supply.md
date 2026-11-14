# Unit Support and Supply

Units have ongoing **support** needs. There are four types: **fuel**, **ammunition**, **food**, and
**medical supplies**. Within an empire's own territory (or an ally's, under a Support Agreement)
supply is implicit; beyond it, a **supply chain** is required.

## Supply sets

A need is stated against a **supply set**, not against one Asset. A set names the Assets that satisfy
it, so a requirement reads "human food" and is met by whatever the tile actually grows — game, berries,
chilli peppers.

- A set carries a **supply type** — `Food`, `Fuel` or `Repair` — which decides what a quantity of it is
  measured against: per individual, per movement point, or per point of damage.
- Each Asset in a set carries an **amount per unit**: how much of that Asset one unit of the need costs.
  Berries are thin rations and cost 1.67 where most foodstuffs cost 1.0.
- A `Repair` set is **drawn from** rather than summed: when damage is taken, one member is picked at
  random, weighted by each member's **selection weight**. A force of tanks taking a hit therefore needs
  steel plate, or circuitry, or tank tracks — whichever the draw lands on.
- A **unit component** states its needs as quantities of sets, and only a **body** may carry any. A
  mounted body needs human food for its rider and a fodder set as its fuel, which is why fodder is a
  `Fuel` set rather than a second food.
- A component carries **at most one requirement per supply type**. The rule is stated per type rather
  than as a list of the types that exist, so one added later is covered; it is also why naming the same
  set twice is refused, that being two requirements of one type. A body owes one ration, not a choice
  between two.
- A body's requirements are **compiled onto its templates**, as a set and a quantity per supply type on
  `CompiledUnitTemplate`. The end-of-turn feed asks this of every Force on the map, and a snapshot is
  read without the per-player unlock filter, so the support forecast can weigh an enemy design's
  appetite the same way it weighs its own. A type the body needs nothing of compiles as null.
- Ammunition is not a supply set. Its options are meant to carry damage profiles of their own, so they
  live on the weapon rather than in a set drawn from blindly — see **Ammunition** below.
- The starting sets are seeded: `human-food` (the edible Foodstuffs Assets, named one by one — Poisons
  and Wild Herbs are left out), `mount-fodder` and `human-medical`. Each member states what it costs,
  so the same Asset may be priced differently in two sets. They are edited in the Entity Setup Tool's
  Supply Sets dialog.

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
  a Support Agreement exists. (Assets are still deducted as support — there is just no chaining
  required.) What counts as an empire's own territory is in
  [campaign-map/tile-ownership.md](../campaign-map/tile-ownership.md).
- A Force in **ambush** or **recon** stance takes nothing from the tile it sits on, so that its
  feeding cannot give it away ([campaign-map/vision.md](../campaign-map/vision.md#hidden-forces-and-food)).
  It must be supplied some other way, which means a concealed stance is only sustainable inside
  friendly territory until supply trains exist.
- An **intact** supply chain exists where units carrying the SUPPLY component are daisy-chained back
  to friendly territory. The chain is **broken** when it is not contiguous; any units beyond the
  break (including supply units) are unsupplied.
- The supply radius of chained units must **overlap or touch** (be adjacent) for the chain to be
  active.
- **Rivers cut supply.** A chain cannot cross a Medium or Large river edge unless a bridge or ferry
  carries it over. Neither bridges nor ferries exist yet. Rivers do not block vision. See
  [unit-movement.md](unit-movement.md#rivers--built) for how rivers are modelled.

## Fuel

- A body states its fuel need as a quantity of a `Fuel` supply set, per movement point. Any one of the
  set's Assets will do, each at its own amount — diesel and petrol are interchangeable at different rates.
- Fuel sets: fodder (mounted units), steam (wood), coal, oil, diesel, marine diesel, petrol,
  airplane fuel, nuclear.
- How fuel limits movement is in unit-movement.md, for [the campaign map](unit-movement.md#fuel) and
  [battle](unit-movement.md#fuel-1).

## Ammunition

- A weapon component declares the kinds of ammunition it accepts — **between 0 and 3** of them — by
  naming an Asset for each. An empty list is a weapon that needs none, which is every melee weapon.
- An attack or a defence spends **one unit** of whichever kind is loaded, so an option carries no rate
  of its own. How dear a round is belongs to the Asset, and a weapon that empties a magazine where a
  bow looses one arrow is expressed by what its Asset counts — a box of bullets rather than a single
  round.
- Declaring several is not carrying several: a weapon accepts up to three kinds and **fires one of
  them**, so what it needs to attack and defend is 0 or 1 ammunition type.
- The options are authored on the weapon itself, in the Entity Setup Tool's component dialog: on the
  Properties page for a Weapon component, and on Attack (Body) for a body's inherent attack. The picker
  offers the Assets under `Weapons.Ammunition` and nothing else, so a weapon cannot be loaded with
  timber or ore.
- Ammunition is not a supply set, because an option is meant to carry a **damage profile** of its own —
  an anti-personnel and an anti-tank shell change what the weapon does, rather than being drawn from
  interchangeably. That profile is not built yet, so for now the options differ only in what they cost.
- Seeded ammunition Assets: sling shot, arrows, bullets (generic) and artillery rounds, under
  `Weapons.Ammunition`. They are ungraded, unlike the weapons themselves. Further types — high-energy
  rounds, piercing rounds, rockets — are authored in the Asset Manager as they are needed.
- A unit **out of ammunition** attacks and defends at **30%** of its normal values.
- A unit may carry multiple weapons and uses the most effective weapon for which it has ammunition.
- Nothing consumes ammunition yet: a Force carries no ammunition stock, so the 30% penalty and the
  best-supplied-weapon choice are design rather than behaviour.

## Food

- A unit base component requires a **food supply set** to remain healthy, at a quantity per individual.
  `human-food` is the set men eat; a mount's fodder is a `Fuel` set, not a food one.
- Healthy and injured individuals each eat a **full ration**: a wound is no cheaper to feed.
- A unit that goes short of food takes **casualties in proportion to its shortfall**. At total
  starvation it loses **34%** of its individuals in one turn; going half short costs half of that. The
  total is rounded **down**, so a large unit carrying a small deficit loses nobody.
- A single roll splits that total between **dead** and **wounded**: between 10% and 30% of the
  casualties die and the rest are wounded, rounded in the wounded's favour. How many suffer is
  fixed by the shortfall; only who dies is left to chance.
- Deaths are taken from the **already wounded** first and reach the healthy only once the wounded
  are gone. Wounds are taken from the healthy — and a wound with no healthy man left to take it
  **kills instead**. A unit left hungry therefore accumulates wounded and then begins losing men.
- Each Force takes its own casualties; a shortfall shared by several units on one tile is split
  between them in proportion to their individuals before any of this is applied.
- If a tile holds **≤ 15 individuals** in total, the units on it take wounds but never deaths. The
  gate is per tile rather than per unit, so splitting an army into small bands does not buy it.
- With the **Mobile Scavenging** technology, a unit takes a percentage of a tile's food as it moves
  *through* it (i.e. it neither starts nor ends its turn there).
- A unit consumes food from the tile in which it **ends** its turn. If that tile doesn't produce
  enough on its own, the excess must come via a supply chain. Tile food replenishes at a set rate;
  if a unit consumes more than remains, it must move or starve.
- What a tile offers is its biome's own Forage, Hunt and Fish — each of which is an Asset — together
  with any MapResource patch standing on it. Whichever of them the required set holds is food, each at
  the set's own price, so a berry patch feeds fewer individuals than the same amount of game would.
  See [campaign-map/food-yield.md](../campaign-map/food-yield.md) and
  [campaign-map/depletion.md](../campaign-map/depletion.md).

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

- Medical supplies are a `Repair` supply set, so the materials a wounded unit needs are drawn from the
  set rather than fixed — see **Supply sets** above.
- When a unit contains wounded, medical supplies aid quicker restoration of health.
- Without medical supplies, casualty rates from battles are higher and restoration is much slower.
