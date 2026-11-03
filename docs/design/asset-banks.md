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
recompute would give it — an unkeyed bank (`Guid.Empty`), or one sharing a key with a bank it cannot reach.

**Balances never move between banks.** The one sanctioned exception anywhere in the model is the camp feeder
push.

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

**Designed, not built.** Nothing in the code connects two settlements yet: there is no road anywhere in the
codebase — no structure, no seed row, no tile feature — and the river and coastline walks are their own
tasks. So every bank is currently a network of one. These are the rules those tasks build to:

* Two of a player's settlements joined by a transport link are **connected settlements**, and share a `NetworkKey`. A connection is transitive, so A–B and B–C puts all three in one network — "which settlements share a key" is a connected-components walk over one player's settlements.
* **Roads** connect only once the player has the Roads technology (**CM-64**, which must introduce the road itself). **Rivers** only after Canoe (**RS-48**). **Coastlines** join two coastal settlements within a per-player sea range that technology raises (**RS-49**).
* Connections count only between a player's **own** settlements. A road running through a neighbour's village connects nothing.
* Connecting and disconnecting — a road built, a road pillaged, a river dammed — are **re-keys**. No balance moves, no bank is created or deleted. A settlement cut off keeps everything it was holding and simply stops reaching the rest.
* A `SettlementTier` of 'camp' attaches to its closest village, which **sets a feeder parent** (**RS-35**): the camp shares the parent's `NetworkKey`, so it can spend from the parent's stock, and pushes its own production to the parent each turn because its own capacity is tiny. Where two villages are equi-distant, if one of them was where the Settler was created from, the camp attaches to **that** village; otherwise the lower `settlementId` wins. The designation is stored, never recomputed — the settler-origin tiebreaker is history that a later recompute cannot see.
* A settlement that is connected to nothing is its own network of one, banking normally.

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

**Where it is paid from:** one settlement's own bank, or every bank the player owns.

### At one settlement

A build order or an upkeep charge happens somewhere, so the expense carries that settlement and is met from
that settlement's own bank. Where that bank does not hold enough, the expense is refused.

> Draining the site's own bank first and taking the remainder from the rest of its network — richest-first,
> rebalancing nothing — is designed and not built (**RS-36**). Adding a transport turn cost for the distance
> the assets travel is a further task of its own (**RS-27**), and is what turns that remainder ordering into
> nearest-first.

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

## Out of Scope

Conquest, which is **RS-34**. Two rules, and both are short — moving a bank is an ownership change and a
re-key, with no balance to divide and no bank to dissolve:

* A conquered settlement's bank changes hands by moving `AssetBank.PlayerId`. Its balances travel with it, in full, and the captured settlement is re-keyed into the conqueror's network — a network of one unless the conqueror actually has a connection to it.
* The losing player's remaining network is re-keyed too, and may split in two: if the captured settlement was the bridge between two halves of their empire, losing it partitions them. A conquest therefore recomputes for **both** players.
