# Asset Bank refactor — working notes

**Temporary.** These notes exist for the duration of the Asset Bank refactor and nothing else. **Delete this file when RS-47 lands**, which is the step that folds everything
into `docs/design/asset-banks.md`. A file describing a state the code has not reached yet is a second source of truth, and `docs/design/` describes how things are *now* — this one
is allowed to look forward only because it is scheduled to die.

The sixteen steps live in Planner, in the **Asset Bank refactor** group under Resource System (`RS-33` … `RS-47`, with `RS-29` at step 6). **Planner is the source of truth for what
each step does and whether it is done.** Nothing here restates a task. What is here is the cross-cutting material a single task description cannot carry: the invariants that span
all sixteen, the alternatives already rejected, the agreed vocabulary, and the order.

## Invariants

These hold across every step. A step that appears to need one broken has found something the design did not anticipate — raise it rather than quietly making an exception.

* **Balances never move between banks**, with exactly one sanctioned exception: camp deliveries (`RS-35`). Connecting, disconnecting, merging and conquering are all re-keys or
  ownership changes, never transfers.
* **`NetworkKey` has exactly one writer** — the connectivity service. It is derived data with no referential integrity, so a second writer means the wrong settlements can reach each
  other and nothing in the schema catches it. A bank arrives as `Guid.Empty` — unkeyed — and the service is what keys it, which is why provisioning calls `AssignKeyToNewBank`
  rather than setting the column itself. **`Guid.Empty` is never grouped on**: it is the absence of a key, not a network two banks share, so an unkeyed bank reaches nothing but
  itself.
* **A settlement's bank is its bank for the settlement's whole life.** `AssetBank.SettlementId` never changes. Ownership moves via `AssetBank.PlayerId`.
* **A zero balance row is deleted.** A missing row and a zero row mean the same thing to every reader, and capacity does not live on the balance row, so a zero row would carry no
  information.
* **Asset preference outranks locality.** In a group expense the player's ordering of the group is walked first, and locality decides only which bank each asset comes out of.
* **Levelling lives on the credit path, not the debit path.** A debit never rebalances; the next credit re-levels by favouring the emptiest banks.
* **Three different mechanisms keep a quantity out of "what I have", and they are not interchangeable**: the strategic stockpile is a different *column* (`StockpiledQuantity`),
  force cargo is a different *bank kind* (`AssetBank.Kind`), and Player/Derived assets are a different *source* entirely (`Asset.StorageScope`). Do not try to unify them.
* **The asset catalogue stays static and seedable.** It has no `GameId` and knows nothing about games or players. Anything per-game or per-opponent is a read-time projection — see
  `AssetSubjectScope.PerOtherPlayer`.

## Decisions already taken, and what was rejected

Each of these was argued out. Re-opening one needs a new reason, not a fresh look at the same trade-off.

| Decision | Rejected alternative | Why |
|---|---|---|
| One bank per settlement, networked by `NetworkKey` | The bank *is* the shared pool, memberships repoint | A pooled row has no per-settlement location, so per-settlement capacity cannot be enforced and a deposit cannot be spread. It also stranded stock on disconnection. |
| `NetworkKey` as a Guid on the bank | An `AssetNetwork` table | A network row would be a permanent row standing for a relationship that lasts only as long as the connection. |
| Delete a balance at zero | Keep zero rows with `LastDepositOnRound` and a cleanup worker | Capacity does not live on the balance row, so a zero row holds nothing. A per-round process whose only job is to delete rows we chose to keep is circular. |
| `StorageClass`, one required class per asset | Declare capacity against MetaCategory / Category / Group nodes | The hierarchy classifies what a thing *is*, for display; storage classifies how it is *kept*. An armoury spans categories, a timber yard spans groups. |
| `StorageClass` as its own catalogue | Reuse `Tag` / `TagType` with a new type | A tag is many-to-many and optional; a storage class is exactly one per asset and required, which only a non-null FK can enforce. `Tag` is also a unit-design concept. |
| Dedicated store, then a generic pool | Full ancestor-chain nested budgets | Two levels keep a refusal explainable. With four nested budgets a player cannot see which one bound. |
| A credit fills in ascending asset id, and a clamp is **reported** rather than reordered (`RS-51`) | A player credit preference, mirroring "asset preference outranks locality" on the debit path; or a proportional split across the contending rows | Ascending asset id is arbitrary — a hunting camp delivering into a full village delivers its bones and loses its meat, because that is the order the catalogue declares them in. But both alternatives add a mechanism to fix the wrong half of the problem: the failure a player actually meets is that the loss is **invisible**. Reporting it is the fix, and it is the one that stays true whichever ordering a later ticket chooses. |
| A credit places what fits and reports the unplaced remainder | Refuse a credit that cannot be placed in full, at the credit or at extraction | A refusal makes a one-unit overflow throw away a whole turn's yield, and a turn has to end either way. Reporting the figure leaves wasting it or refusing before the next extraction with the caller, and it stays true whichever a later ticket chooses. The pre-flight for refusing before extraction is the same `SettlementStorageCapacity` the credit places through, so the two cannot disagree. |
| A camp delivery credits the parent's **network**, with every camp bank excluded | Credit the parent's bank alone, as `RS-29` clamped to | Stranding stock in a camp because one village is full is the stranding problem the network exists to solve, and a village two roads away is still a large store. Excluding camps is the same rule as "another camp is never a parent" — moving a small store into a small store gains nothing — and it excludes the delivering camp as a consequence rather than as a special case. |
| Local-first debit, remainder richest-first | Level the whole network on every settlement expense | Levelling on the debit path did work the credit path does for free, and it ignored that goods already on site do not travel. |
| Unit banks attach to `UnitInstance`; a Command is a network of its units' banks | A bank that points at either a Unit or a Command | Force is a category, not a type — there is no shared table. Capacity is per-Unit anyway, and `ForceOrderParticipant` sets the precedent. |
| Stockpile as a reserved column on the balance | A player-level stockpile store, or a separate reserve table | Every bank is a settlement bank; the reserve physically sits where its building stands. A second column is invisible to existing readers by construction, so "not in totals" costs nothing. |
| `PopulationSegment (AssetBankId, AssetId, NationalityPlayerId, Quantity)` | A `PopulationType` enum beside the asset id | The status *is* the catalogue row, which also supplies the name, icon and `Asset.Weight`. An enum would be a second source of truth for what kinds of people exist. |
| Population keyed on `AssetBankId` | Locating people by settlement or unit id directly | Keying both stores on the bank makes every "what is here, what does it weigh, who owns it now" question a union over two tables of the same shape. |
| Own people in the same table as foreign-held | Foreign-held only | `MyPeople.Interned` is *our* people in *someone else's* camps — a row whose location is an enemy bank and whose nationality is us. It cannot be expressed otherwise. |

## Vocabulary

Use these names. Sixteen sessions inventing sixteen synonyms is the main thing these notes exist to prevent.

| Term | Meaning |
|---|---|
| **Network** | The set of banks sharing a `NetworkKey`. What a settlement can draw on. Also what a Command's units' banks form. |
| **Re-key** | Changing which network a bank belongs to. Never moves a balance. |
| **Parent settlement** | The settlement a camp delivers its production to each turn (`Settlement.ParentSettlementId`). Stored, never recomputed. The camp delivers; the parent only receives, and its stock funding the camp's construction is the shared network rather than a flow back. |
| **Storage class** | How an asset is kept, as against what it is. `Asset.StorageClassId`, required, exactly one. |
| **Building** | Something inside a settlement, as against a **structure**, which stands on a campaign map tile. Only a building grants a dedicated store; a structure never does. See "Buildings" in `docs/design/settlements.md`. |
| **Dedicated store** | Capacity granted for one storage class by a building — the armoury, the oil dump. |
| **Generic pool** | Tier-granted capacity any class overflows into. The storehouse. |
| **Sundries** | The catch-all *storage class*, for goods that earn no store of their own. Not the generic pool: that is the storehouse, and the two words are not interchangeable. |
| **Local-first** | A settlement expense drains the site's own bank before touching the rest of its network. |
| **Levelling** | Cutting the fullest banks to a common water-line (debit), or filling the emptiest first (credit). |
| **Reserved** | `AssetBankBalance.StockpiledQuantity` — held back from circulation and from every total until released. |
| **Cargo** | What a unit bank holds. Excluded from empire totals until deposited, even inside a settlement. |
| **Segment** | One `PopulationSegment` row: a quantity of one status, of one nationality, in one bank. |

## Order

```
RS-33  bank per settlement + NetworkKey   ─┬─> RS-34  conquest re-key
                                           ├─> RS-35  camp deliveries
                                           ├─> RS-36  local-first debit
                                           ├─> RS-42  unit banks
                                           ├─> CM-64  roads, and road networking
                                           ├─> RS-48  river networking
                                           └─> RS-49  coastline networking
RS-37  StorageClass ──> RS-29  settlement capacity ──┬─> RS-38  credit spreading
                                                     ├─> RS-39  stockpile store ──> RS-40  building, diversion, release
                                                     └─> RS-42  unit banks
RS-41  Asset.Weight ────────────────────────────────────> RS-42  unit banks ──> RS-43  cargo orders
RS-42 + RS-33 ──> RS-44  PopulationSegment ──┬─> RS-45  collapse duplicated population codes
                                             └─> RS-46  migrate Settlement.Population
everything ──> RS-47  consolidate docs/design/asset-banks.md, and delete this file
             ──> RS-50  review CD-6, CD-7, CD-8 and close what is spent
```

**`RS-37` and `RS-41` depend on nothing** and can be picked up at any point. Everything else wants its predecessors in place.

**`CM-64`, `RS-48` and `RS-49` are the three transport tasks, and they sit outside the sixteen** — each is standalone and ungrouped, and each fills the same seam: the
connectivity service `RS-33` introduces computes isolation, and every bank is its own network of one until one of the three gives it something to walk. Each is gated on its own
technology. `CM-64` carries the most, because no road exists anywhere in the codebase and it has to introduce one before it can network anything.

## Questions deliberately left open

Recorded on the task that will hit them, repeated here so they are not re-discovered from scratch.

* Whether carried prisoners share the one weight budget with goods or get their own (`RS-43`). Sharing is simpler and makes a loot-laden force unable to also take captives.
* What happens to reserved quantity when its Strategic Stockpile is razed (`RS-40`).
* Whether a player can see their own people held in an enemy's camps, given those rows live in the enemy's bank (`RS-47`). An intelligence question, but `MyPeople.Interned` is
  unlocked from round one.
