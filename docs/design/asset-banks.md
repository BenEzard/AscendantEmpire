# Asset Banks

The purpose of this document is to describe the game concept of Asset Banks.

An Asset Bank is one settlement's store of assets. A settlement can also draw on every other bank in its
**network**, which is the set of banks sharing a `NetworkKey`.

See also `assets-data-model.md` which describes the asset model.

## The model

Two tables in the `assets` schema, keyed on the player:

| Table | Key | What it holds |
|---|---|---|
| `AssetBank` | `Id` | One settlement's store, owned by a `Player`. Carries `SettlementId` (unique) and the `NetworkKey` that says what it can reach. |
| `AssetBankBalance` | `(AssetBankId, AssetId)` | How much of one asset that store holds. |

**One bank, one settlement, for the settlement's whole life.** `AssetBank.SettlementId` never changes, and
the unique index on it is what makes "a settlement has exactly one Asset Bank at any time" a schema rule —
a second bank for the same settlement cannot be inserted. A conquest moves `AssetBank.PlayerId` instead, so
a settlement's stock travels with the settlement.

Per-settlement capacity is what forces this shape: a cap needs a per-settlement row to bind to, and a
deposit needs per-settlement rows to spread across. Keeping a settlement's stock with the settlement is
also what makes a severed road harmless — the settlement still holds everything it had, and only loses its
reach.

**Sharing is the key, not the bank.** Banks carrying the same `NetworkKey` may draw on each other, so
connecting or disconnecting settlements is a **re-key**: no balance row is touched, and no bank is created
or deleted. There is deliberately no network table — a row standing for a relationship that lasts only as
long as the connection does would outlive what it describes.

`NetworkKey` is derived data with no referential integrity, so a stale key would let the wrong settlements
reach each other and nothing in the schema would catch it. Two things mitigate that: it has exactly one
writer, the connectivity service, and that service can also report any bank whose key disagrees with what a
recompute would give it — an unkeyed bank (`Guid.Empty`), one sharing a key with a bank it cannot reach, or
one sitting in a network of its own when it should be sharing one.

**Balances never move between banks.** The one sanctioned exception anywhere in the model is a camp delivery.

**Every bank is a settlement bank.** There is no player-level stockpile variant. Such a bank would have no
settlement to point at and would have to be excluded from every settlement query — a bank in name only.
What it was reaching for belongs on the asset instead, below.

`Quantity` is a `long` scaled by `ScaledAmount.Scale` (1,000), like every other amount in the game, so a
fractional per-turn yield accumulates without being rounded away at the balance.

## Where a balance lives

`Asset.StorageScope` says where an asset's figure comes from. It is a property of the asset, not of a
phase of the game: tangible stock moves into a settlement's bank as soon as the first settlement is
founded, while experience points stay player-level for the whole game, so the three have to coexist from
turn one.

| Scope | Meaning |
|---|---|
| `SettlementBank` | The default, and nearly everything. Held in `AssetBankBalance`, subject to connectivity. |
| `Player` | Held per player for the whole game. Never in a bank, unaffected by roads and rivers. |
| `Derived` | No balance row at all. The read path resolves the figure from whatever already records it. |

The two Experience assets are `Player`: `PlayerResearchState.UnspentDirectedPoints` and
`UnspentDiffusionPoints` already hold them, with `PlayerUnusedExperience` for the typed-per-kind
remainder. They get no balance row, because a second store would be a second source of truth for one
number — with the research pipeline writing the one the panel is not reading.

The Population and ForeignPopulation assets are `Derived`: Civilians from `Settlement.Population`, the
three services from the force rosters, and Slaves / Interned / P.o.W. from the holdings ledger.

This is what lets a drawdown route a debit without hardcoding which assets are not spendable from a bank,
and it keeps the catalogue row a pure label.

## Asset Bank Rules

**Built:**

* Every settlement is born with an Asset Bank, whatever its `SettlementTier` — a 'camp' (1) included. A camp is the tier the first settlement of a game is, and the first building produces in one, so it banks for itself rather than drawing on a village it may not have.
* A settlement has exactly one Asset Bank, for its whole life. Every Asset Bank has exactly one settlement, which may be of any tier.
* A bank is born into a network of its own, and the connectivity service is the only thing that writes `NetworkKey`.
* A `SettlementTier` of 'camp' attaches to a parent settlement when it is founded, which both puts it in that settlement's network and makes it deliver its production there each turn — see [Camp deliveries](#camp-deliveries).

**One connection is built, and it is not a transport link.** A camp shares its parent settlement's `NetworkKey`,
so those two banks reach each other. Nothing else in the code connects two settlements: there is no road anywhere
in the codebase — no structure, no seed row, no tile feature — and the river and coastline walks are their own
tasks. So every other bank is a network of one. These are the rules those tasks build to:

* Two of a player's settlements joined by a transport link are **connected settlements**, and share a `NetworkKey`. A connection is transitive, so A–B and B–C puts all three in one network — "which settlements share a key" is a connected-components walk over one player's settlements.
* **Roads** connect only once the player has the Roads technology (**CM-64**, which must introduce the road itself). **Rivers** only after Canoe (**RS-48**). **Coastlines** join two coastal settlements within a per-player sea range that technology raises (**RS-49**).
* Connections count only between a player's **own** settlements. A road running through a neighbour's village connects nothing.
* Connecting and disconnecting — a road built, a road pillaged, a river dammed — are **re-keys**. No balance moves, no bank is created or deleted. A settlement cut off keeps everything it was holding and simply stops reaching the rest.
* A settlement that is connected to nothing is its own network of one, banking normally.

## Camp deliveries

A camp keeps its own bank and collects into it, and delivers everything it collects to a parent settlement each
turn. This is the one sanctioned exception to balances never moving between banks — an explicit per-turn
transfer, not churn from a graph re-key.

**Why a delivery is needed at all.** A camp shares its parent's `NetworkKey`, so it can already *spend* from the
parent's stock without anything moving. What it cannot do is *store*: a camp's capacity is tiny, so its own
production would hit the cap and be wasted. The delivery exists to get stock out of a small store and into a
large one.

**Only one of the two directions is a delivery.** The camp's delivery *moves balances* — rows leave its bank and
arrive in its parent's, once a turn. The parent's stock funding the camp's construction moves nothing at all:
the camp spends out of a pool the two share because they carry one `NetworkKey`. So the camp delivers, and the
parent only ever receives.

**Who the parent is.** The nearest settlement of a tier above camp that the player still holds, by wrap-aware
map distance; where two are equidistant, the lower `settlementId` wins. Ownership is read off the structure, so
a village that has changed hands is not offered to its founder's camps. Another camp is never a parent, because
delivering a small store into another small store would gain nothing.

> The design puts a third rule between those two: where two candidates are equidistant and one of them is where
> the settler was created from, the camp attaches to **that** one. Nothing records which settlement a force was
> raised at, and nothing produces forces at a settlement yet, so that middle rule is not applied.

**The parent is stored, not recomputed.** `Settlement.ParentSettlementId` carries it, written once when
the camp is founded. The settler-origin rule above is history, so a later recompute could not reproduce it and
would quietly re-attach the camp to the other candidate.

**A delivery runs only when the deliverer is a camp, its designated parent is not, *and* the two banks share a
`NetworkKey`.** Sever the link and delivery simply stops: the camp keeps collecting to its own cap and loses
nothing it was holding, and the designation does not change. Reconnect and it resumes. A camp with no parent at
all is its own network of one and banks normally — the first settlement of a game is a camp, and the Meeting
Place is built in one.

Both tiers are checked at delivery time rather than trusted from whatever set the designation, because nothing
clears the column when a settlement changes tier. A settlement that grew out of camp tier would otherwise keep
emptying itself into its old parent, and a camp designated to another camp would be a chain — emptying the
middle one first destroys the stock behind it rather than moving it on.

**Deliver everything, keep nothing.** The camp is left holding no balance row at all, which costs it nothing while
it still draws on the network it shares. The accepted consequence runs the other way: a severed camp fills its
small store and then wastes production, and that is the intended pain.

It runs at the end of a turn, after production and after construction, so a camp spends on its own build order
out of its own bank before the remainder is swept to the parent. Running it twice in one turn moves the same
stock once, because a camp emptied by the first run has nothing left for the second.

## Conquest

A settlement changing hands moves three things, and the third is the one that is easy to miss.

* **The bank changes owner, and nothing else.** `AssetBank.PlayerId` moves to the conqueror. `SettlementId` does not move, no
  bank is created or dissolved, and no balance row is read or divided — so the conqueror receives the full stock simply because
  the stock never went anywhere. There is no pillage wastage.
* **The captured settlement is re-keyed** into a network of its own, and the conqueror's recompute is what joins it to whatever
  they can actually reach — which is nothing today, since no transport link exists yet. Keeping the loser's key instead would
  leave the captured bank sharing a network across the ownership boundary, and connectivity is assessed one player at a time, so
  no consistency check could ever see it.
* **The losing player's remaining network is recomputed too, and may split in two.** If the captured settlement was the bridge
  between two halves of their empire, losing it partitions them. A conquest therefore recomputes for **both** players.

`IAssetBankConquestService` is where this happens, and it is the one asset service that saves. The connectivity service reads
ownership from the database rather than from the change tracker, so the hand-over has to be committed before either recompute
runs; both saves land inside one transaction, and a recompute that fails takes the hand-over back with it.

**Nothing calls it yet.** No siege or capture exists in the code, so taking a settlement has to go through this service *and*
through `IStructureLifecycleWriter.CaptureAsync`, which is what moves who holds the structure. Wiring one without the other
leaves a settlement whose new owner cannot spend its stock, or stock a defeated player is still spending.

## Querying requirements
The game will need to be able to query on a specific Asset Bank, and also query on all Asset Banks.

## Drawing down an asset

An expense makes two choices, and both change what is spent.

**What it names:**

* An **`Asset`** — "10 Oak Logs". Only that asset is touched, whatever its group's ordering says, and a
  shortfall on it fails the expense rather than falling through to the rest of the group: a caller naming
  Oak has already decided it does not want Cedar.
* An **`AssetGroup`** — "10 Logs, any kind". Spent in the player's ordering of the group, each asset taken
  to nothing before the next is touched. With an ordering of Oak, Generic, Cedar and 4 Oak and 20 Generic
  held, an expense of 10 takes Oak to zero and the remaining 6 out of Generic, leaving Cedar alone.

**Where it is paid from:** the network one settlement sits in, or every bank the player owns.

### At one settlement

A build order or an upkeep charge happens somewhere, so the expense carries that settlement. It drains
**that settlement's own bank first**, then takes the remainder from the rest of its network — the banks of
the same player sharing its `NetworkKey`. An unkeyed bank reaches nothing but itself, so a settlement the
connectivity service has not keyed pays out of its own stock alone.

What is available is the **sum across that network**, and the expense is refused only where the banks
between them do not hold enough. A settlement with no bank of its own cannot be spent at however much its
network holds: the expense happens there.

**The debit moves nothing and rebalances nothing.** No bank is topped up and the settlement that just paid
stays depleted. Levelling lives on the credit path instead: next round's production favours the emptiest
banks, so the settlement that paid for a building takes a larger share and the network re-levels over time
(**RS-38**).

The remainder is **levelled across the rest of the network**, which takes it from the richest banks first —
the same spread, and the same reason, as the Empire-wide arm below: a settlement with little keeps the stock
its own construction will need.

**Asset preference outranks locality.** For a group expense the walk is, for each asset in the player's
ordering: the site's own bank, then the remainder across the network. The site's Cedar is not taken before
the network's Oak.

> Adding a transport turn cost for the distance the assets travel is a task of its own (**RS-27**). It is
> the remainder ordering, not the reaching, that it turns into nearest-first, with the site at distance zero.

### Empire-wide

A technology cost or an export belongs to no settlement, so the expense names none and is met from
everything the player holds anywhere. What is available is the **sum across every bank**: two banks that
cannot reach each other are added together here, because nothing is travelling to a building site. An
Empire-wide expense carries **no transport cost**, ever.

**The debit is spread by levelling.** The fullest banks are cut down to a common water-line and the rest
are left alone, rather than every bank paying a proportion of the cost. With banks holding 50, 30 and 4
and a cost of 40, the line falls at 20: the first two are cut to 20 each and the third pays nothing.

Levelling rather than proportional because it takes from the richest first, so a bank with little keeps
what it has and stays able to fund its own construction. A proportional spread would take something out
of every bank on every Empire-wide purchase, and so would regularly leave a small settlement unable to
build what it could have built a moment earlier.

A quantity is a scaled integer, so a water-line that divides exactly is the exception. The leftover raw
units go to the richest bank first and then to the lower bank id, so the same expense against the same
banks always spends the same rows.

**A group expense orders across the empire, not within a bank.** Every bank's Oak is taken to nothing
before any Cedar is touched anywhere: the player's ordering says which asset they are willing to lose
first, and that holds at the scale the expense is paid at. Within each asset of that walk, the take is
levelled across the banks holding it.

**The toolbar reads this same sum.** `GET /player/assets` reports it as an asset's `amount`, so the figure
on a ticker and the figure an Empire-wide purchase is met from are one number. The ticker is rounded down,
which is also all a drawdown would release of a part unit. What one settlement can spend on a build order is
the narrower figure, and `GET /player/asset-banks/{settlementId}` is what reports it: every bank in that
settlement's network, with the settlement's own bank named as the site, since the site is what a build order
spends first.

### Either way

* An expense succeeds **whole or changes nothing**. Sufficiency is settled across the entire expense
  before a single balance is touched, so a group expense that zeroes Oak and then finds Cedar short never
  happens.
* A refusal says what was short and by how much, and **nothing in it is bank-specific**. An Empire-wide
  expense is short against everything the player holds, and naming the bank that ran out would point at a
  boundary the expense was never spent across.
* A balance taken to nothing is **deleted** rather than left at zero. A missing row and a zero row mean
  the same thing to every reader.
* An asset whose `StorageScope` is `Player` can only be spent Empire-wide — a figure held once for the
  whole game is not spent anywhere in particular. Naming a settlement for one is a caller error.
