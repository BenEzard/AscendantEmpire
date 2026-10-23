# Asset Banks

The purpose of this document is to describe the game concept of Asset Banks.

An Asset Bank is a collection of assets which one or more settlements has access to.

See also `assets-data-model.md` which describes the asset model.

## The model

Three tables in the `assets` schema, keyed on the player:

| Table | Key | What it holds |
|---|---|---|
| `AssetBank` | `Id` | One pool, owned by a `Player`. Carries a `NetworkKey` token that nothing reads yet. |
| `AssetBankBalance` | `(AssetBankId, AssetId)` | How much of one asset the pool holds. |
| `AssetBankSettlementMembership` | `SettlementId` | Which bank a settlement draws on. |

**One bank, many settlements.** Sharing is the bank itself, and a membership is what joins a settlement
to it — so connecting two settlements repoints a membership, and merging two banks is folding one's
balances into the other and deleting it. There is no separate "bank group" table: a group row would
stand for a relationship that lasts no longer than the connection does.

Keying the membership on `SettlementId` alone is what makes "a settlement has exactly one Asset Bank at
any time" a schema rule — a second bank for the same settlement cannot be inserted.

**Every bank is a settlement bank.** There is no player-level stockpile variant. Such a bank would never
merge, never split, hold no memberships and have to be excluded from every settlement query — a bank in
name only. What it was reaching for belongs on the asset instead, below.

`Quantity` is a `long` scaled by `ScaledAmount.Scale` (1,000), like every other amount in the game, so a
fractional per-turn yield accumulates without being rounded away at the balance.

## Where a balance lives

`Asset.StorageScope` says where an asset's figure comes from. It is a property of the asset, not of a
phase of the game: tangible stock moves into a settlement's bank as soon as the first village is founded,
while experience points stay player-level for the whole game, so the three have to coexist from turn one.

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
* Asset Banks apply to settlements of `SettlementTier` 'village' (2) or higher. They do not apply to settlements of type 'camp' (1).
* A settlement should have exactly one Asset Bank at any time.
* An Asset Bank should have one or more Settlements to which it belongs, which may be of any tier.
* `SettlementTier` of 'camp' should attach to their closest 'village'. Where two villages are equi-distant, if one of them was where the Settler was created from, then the camp should attach to **that** village. Otherwise, choose the lower `settlementId`.
* When a settlement is connected via road or river to another settlement, then they are **connected settlements**, and share an Asset Bank. A settlement newly-connected, joins the existing Asset Bank. (A river only counts as a connection for the Player after 'Canoe' technology has been completed, and a road only after the Roads technology).
* For the purposes of Asset Banks, connections only count for a player's own settlements, not for other players.
* When a settlement that *was* connected is disconnected (via pillaging of a road or damming of a river), then a new Asset Bank should be created for them. This Asset Bank would start out empty. When the connection is restored, then the temporary Asset Bank should be dissolved, moving all assets into the shared asset bank.
* When a settlement is not connected, then it gets its own Asset Bank.

## Querying requirements
The game will need to be able to query on a specific Asset Bank, and also query on all Asset Banks.

## Drawing down an asset
* When the requested amount does not exist, then a user-friendly error should be returned.
* When an expense is requested to be deducted from an AssetBank it will either be expressed as an `AssetGroup` (i.e. I need 10 Logs, and I don't care which type of logs), or as a specific `Asset` (i.e. Oak log).
* When an expense is requested in an `AssetGroup` then it should be spent based on the player's ordering of asset within the group. (e.g. if the Player has Oak, Cedar, Generic) in their AssetGroup of Logs then it should be deducted in that order. If the amount requested can't be met by what's in Oak, then Oak should be zeroed, then the remaining amount should come from Cedar, then Generic.
* When an expense is requested as an `Asset`, then only that `Asset` should be used, irrespective of the AssetGroup ordering.

## Out of Scope
* If a camp is conquered, then it should change ownership to the conqueror, and change Asset Bank to the conqueror's nearest settlement.
* Where a camp is attached to a village, and the village is conquered, then calculate the ownership boundaries. If the the camp is still in the original owners territory, then find the nearest settlement of the Player. If the ownership boundaries change, and the camp is now in another Player's territory, then auto-destroy the camp.
