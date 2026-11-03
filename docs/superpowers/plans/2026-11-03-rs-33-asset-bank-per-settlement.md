# RS-33: One Asset Bank Per Settlement, Networked By NetworkKey — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make an `AssetBank` one-to-one with its settlement for the settlement's whole life, and make `AssetBank.NetworkKey` the thing that says which banks can draw on each other.

**Architecture:** `AssetBank` gains a required unique `SettlementId`, which does in schema the one job `AssetBankSettlementMembership` existed for, so that table goes. Sharing stops being the bank and becomes a shared `NetworkKey`, written by exactly one service — `AssetNetworkConnectivityService` — so that connecting and disconnecting settlements will be a re-key rather than a merge, and no balance ever moves between banks. The settlement read widens from one bank to the network the settlement sits in.

**Tech Stack:** .NET 9 / EF Core (SQL Server), FluentValidation, NUnit; React + TypeScript (untouched here — nothing in the frontend reads either asset-bank endpoint yet).

**Spec:** This plan is the spec of record. The cross-cutting invariants, the rejected alternatives and the agreed vocabulary are in [docs/work-in-progress/asset-bank-refactor.md](../../work-in-progress/asset-bank-refactor.md); the current rules are in [docs/design/asset-banks.md](../../design/asset-banks.md), which Task 7 rewrites.

**Planner task:** RS-33 — step 1 of 16 in the Asset Bank refactor. Everything else in the group depends on it.

## Global Constraints

- Braces on `if`/`for` always, even single-line bodies, and on their own line.
- C#: private fields `_camelCase`, constants `UPPER_SNAKE_CASE`, private constants both (`_MAX_X`). Locals and parameters stay `camelCase`. Test projects included.
- LINQ in method syntax, not query syntax. Project through mapped navigations and let EF expand the join rather than writing `.Join(…)`.
- 1–2 line `<summary>` with the tags on their own line, rationale in `<remarks>`/`<para>`; `<param>` only where the parameter is not obvious. Comments sparing and short — a line where the code cannot show why, never a paragraph.
- Every test carries `[Test, Description("…")]` — one line, stating what it pins and why. Test names read `Should<Outcome>When<Condition>`.
- Lightest sufficient test tier: no database → EF InMemory → containerised SQL Server. See [docs/testing.md](../../testing.md). Everything new here is in-memory; the only containerised work is swapping assertions in two existing `SettleForceHandlerTests` cases.
- The tier-3 container pins port 14330, so runs collide with a Visual Studio run. Always use a targeted `--filter`; never run the full suite unprompted.
- Never `dotnet test -v q`, and never pipe a test run through `tail` — grep for failure markers instead.
- Scaffold migrations with `--project AscendantEmpire.Application --startup-project AscendantEmpire.Api`. Never pass `--no-build`. **Never run `database update`.**
- **Each task ends in its own commit**, subject prefixed `RS-33:`. Claude never runs git and never applies a migration: the commit message is handed to Ben inline as a fenced block and he commits.

## Decisions Taken

Recorded here because the implementer cannot derive them:

1. **The connectivity service computes isolation, and nothing more.** No road exists anywhere in the codebase — no `Structure`, no seed row, no tile feature — so a road graph cannot be walked. Rivers and coastlines could be, but are their own tasks: **CM-64** (roads, which must introduce the road first), **RS-48** (rivers, gated on `Canoe`) and **RS-49** (coastline, gated on a per-player sea range). This step builds the seam those three fill and gives every bank its own network of one. Nothing regresses, because no merge, split or repointing exists in code today either.
2. **`SettlementId` is non-nullable now.** RS-42 gives `UnitInstance`s banks with an `AssetBank.Kind` discriminator and owns the migration that relaxes this column. Nine steps of a real not-null guarantee is worth more than saving RS-42 one `AlterColumn`.
3. **`NetworkKey` loses its `Guid.NewGuid()` initialiser.** A bank arrives as `Guid.Empty` — unkeyed — and the connectivity service is what keys it. That is what makes "exactly one writer of `NetworkKey`" true from this step rather than nine steps later, and it gives the consistency check something real to find.
4. **The settlement read returns the network, not the bank.** `GET /player/asset-banks/{settlementId}` is documented as what this settlement and everything connected to it can spend, and under the new model that is a set of banks. The route does not change.
5. **The drawdown's settlement arm still spends from the site's own bank alone.** Spending across the network is **RS-36** and is not started here. Nothing regresses: no bank has ever pooled more than one settlement in practice.
6. **No data is carried over.** Every `AssetBank`, `AssetBankBalance` and membership row is cleared before the migration runs. Ben clears them with SQL this plan hands him; the migration itself ships no `DELETE`.
7. **No containerised test for the unique index.** The provisioning service's own duplicate guard is pinned in-memory, and the index is visible in the model snapshot and the migration.
8. **`asset-banks.md` states the connect, disconnect and camp-attach rules in the new vocabulary and says plainly that none of them is built**, naming CM-64, RS-48, RS-49 and RS-35. The doc describes the current state; pretending the behaviour exists is what it does today.

## Review Focus

Failure modes this design implies that no task's happy path exercises. Each has its test assigned to the task that owns the code:

1. An **unkeyed bank** (`Guid.Empty`) must never be grouped with another unkeyed bank. Two freshly staged banks of one player both read as the same network otherwise, which is exactly the stale-key fault the single-writer rule exists to prevent. → Task 3.
2. Two players' banks must not be grouped by `NetworkKey` alone. The index is `(PlayerId, NetworkKey)`, so the **query must filter on `PlayerId` too** — a Guid collision, or a conquest caught mid-recompute, otherwise lets one player read another's stock. → Task 3.
3. `GetForSettlementAsync` must answer **one 404 for both "no bank" and "not yours"**. The old code got that free from the membership's `Bank` navigation being an INNER JOIN; the new code filters two columns and could easily leak the difference. → Task 3.
4. **`RecomputeForPlayerAsync` must be idempotent and must not throw for a player with no settlements.** RS-34 calls it twice in one conquest — once per player — and a player can be mid-game with nothing founded. → Task 2.
5. **Provisioning a settlement that already has a bank** must refuse rather than stage a second. In-memory enforces no unique index, so the service's own guard is the only thing standing between a double-founding and two banks for one settlement. → Task 1.

---

### Task 1: `AssetBank.SettlementId` and the network index

The column and the indexes, with `AssetBankSettlementMembership` still in place so every existing read keeps working. Task 6 retires it.

**Files:**
- Modify: `AscendantEmpire.Domain/Entities/Assets/AssetBank.cs`
- Modify: `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` (the `AssetBank` block, ~line 693)
- Modify: `AscendantEmpire.Application/Features/Assets/Services/AssetBankProvisioningService.cs`
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankTestBase.cs`
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankProvisioningServiceTests.cs`

**Interfaces:**
- Produces: `AssetBank.SettlementId` (`int`), `AssetBank.Settlement` (`Settlement`, required navigation). `AssetBankTestBase.CreateBankAsync(int playerId, int settlementId, IReadOnlyDictionary<string, long> balances, Guid? networkKey = null)` — the settlement-id collection becomes a single id, and the key is settable so a network fixture can be built.

- [ ] **Step 1: Change the test base's bank helper**

`CreateBankAsync` takes one `settlementId` instead of `IReadOnlyCollection<int> settlementIds`, and an optional `networkKey` which defaults to a fresh `Guid.NewGuid()` when null. It sets `SettlementId` on the bank **and** still adds the `AssetBankSettlementMembership` row, so the membership-based reads under test stay satisfied until Task 3.

Update the `<remarks>` paragraph that explains why no `Settlement` rows are written — the reason is unchanged (the FK is not enforced in-memory) but the wording says "a membership's Settlement foreign key".

- [ ] **Step 2: Update every `CreateBankAsync` call site to the new signature**

Mechanical: `[_SETTLEMENT_ID]` becomes `_SETTLEMENT_ID`. Across `AssetBankReaderTests`, `AssetBankQueryHandlerTests`, `AssetBankDrawdownServiceTests`, `GetPlayerAssetsHandlerTests` and `PlayerAssetQuantityReaderTests`.

Two calls need a decision rather than a rewrite:
- `AssetBankReaderTests.ShouldReturnTheBanksHoldingsAndMembershipsForOneSettlement` passes `[_SETTLEMENT_ID, _CONNECTED_SETTLEMENT_ID]`. Split it into two banks sharing one `networkKey` — it becomes the network fixture Task 3 needs. Leave its assertions alone for now; Task 3 rewrites them.
- `AssetBankReaderTests.ShouldReturnABankThatHasNoMemberships` passes `[]`. A bank with no settlement is no longer a state the model permits, so **delete this test**. Its premise — an orphaned bank still holds stock until it is folded away — is a fact about a model this step removes.

- [ ] **Step 3: Write the failing test for the duplicate guard reading `SettlementId`**

In `AssetBankProvisioningServiceTests`, replace the membership-based arrangement at line 175 with a bank carrying `SettlementId`:

```csharp
[Test, Description("Provisioning a Settlement that already has a bank is refused, because a second bank for one Settlement is a caller mistaking a re-key for a founding.")]
public async Task ShouldRefuseWhenTheSettlementAlreadyHasABank()
{
    // arrange: a saved Settlement id with an AssetBank already pointing at it
    // act: ProvisionForNewSettlementAsync for that same Settlement
    // assert: result.HasSucceeded is false, and readContext.AssetBanks has exactly one row for that SettlementId
}
```

- [ ] **Step 4: Run it to verify it fails**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankProvisioningServiceTests"`
Expected: FAIL — the guard still queries `AssetBankSettlementMemberships`, which the new arrangement does not write.

- [ ] **Step 5: Add the column, the navigation and the EF configuration**

On `AssetBank`: `public int SettlementId { get; set; }` and `public Settlement Settlement { get; set; } = null!;`. Rewrite the class `<summary>` and the "One bank, many settlements" `<para>` — the bank is now the settlement's own store for the settlement's whole life, and sharing is the `NetworkKey`. Rewrite the `NetworkKey` `<remarks>`, which currently says nothing reads it. Leave `SettlementMemberships` in place for Task 6.

In the `AssetBank` block of `ConfigureAssetsSchema`:
- `HasOne(x => x.Settlement).WithOne().HasForeignKey<AssetBank>(x => x.SettlementId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AssetBank_Settlements_SettlementId")`. No navigation back from `Settlement`, matching how the membership was configured — the assets schema reaches into settlements, not the other way about.
- `HasIndex(x => x.SettlementId).IsUnique().HasDatabaseName("UX_AssetBank_Settlement")` — this is the whole of what the membership's primary key was doing.
- `HasIndex(x => new { x.PlayerId, x.NetworkKey }).HasDatabaseName("IX_AssetBank_Player_NetworkKey")`.
- **Delete `IX_AssetBank_Player`** — `PlayerId` is the new index's left-most column, so it is redundant.

Update the `// --- Asset banks ---` banner comment, which describes a bank outliving a settlement's membership of it.

- [ ] **Step 6: Point the provisioning guard and the staged bank at `SettlementId`**

The guard queries `context.AssetBanks.AsNoTracking().Where(bank => bank.SettlementId == settlement.Id)`. The staged bank sets `Settlement = settlement` — the navigation, not the id, because the `Settlement` is routinely still unsaved here — and **still** stages the membership. Its comment says so and names Task 6.

- [ ] **Step 7: Run the asset-bank tests**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Features.Assets"`
Expected: PASS.

- [ ] **Step 8: Commit** — hand Ben the message.

---

### Task 2: `AssetNetworkConnectivityService`, the one writer of `NetworkKey`

**Files:**
- Create: `AscendantEmpire.Application/Features/Assets/Services/IAssetNetworkConnectivityService.cs`
- Create: `AscendantEmpire.Application/Features/Assets/Services/AssetNetworkConnectivityService.cs`
- Create: `AscendantEmpire.Application/Features/Assets/Classes/AssetNetworkFault.cs`
- Create: `AscendantEmpire.Domain/Enums/Assets/AssetNetworkFaultKind.cs`
- Modify: `AscendantEmpire.Domain/Entities/Assets/AssetBank.cs` (drop the `NetworkKey` initialiser)
- Modify: `AscendantEmpire.Application/ApplicationStartup.cs` (~line 167, beside the provisioning registration)
- Test: `AscendantEmpire.Tests/Features/Assets/AssetNetworkConnectivityServiceTests.cs`

**Interfaces:**
- Consumes: `AssetBank.SettlementId` and `AssetBank.NetworkKey` from Task 1.
- Produces:

```csharp
internal interface IAssetNetworkConnectivityService
{
    void AssignKeyToNewBank(AssetBank bank);

    Task<ServiceResultBase> RecomputeForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);

    Task<ServiceResult<IReadOnlyCollection<AssetNetworkFault>>> FindFaultsForPlayerAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}

public sealed record AssetNetworkFault(int AssetBankId, int SettlementId, AssetNetworkFaultKind Kind);

public enum AssetNetworkFaultKind { Unkeyed = 1, SharedKey = 2 }
```

`AssignKeyToNewBank` is synchronous and takes the entity, because provisioning stages an unsaved bank — a database recompute could not see it, and a second `SaveChangesAsync` would let a settlement be saved unkeyed. `RecomputeForPlayerAsync` stages onto the caller's context and does not save, matching `IAssetBankProvisioningService`.

- [ ] **Step 1: Write the failing tests**

New in-memory fixture deriving from `AssetBankTestBase`. Six cases:

```csharp
[Test, Description("A newly staged bank is given a key by the connectivity service, since nothing else may write NetworkKey.")]
public void ShouldAssignAFreshKeyToANewBank()
// assert: bank.NetworkKey is not Guid.Empty

[Test, Description("An unkeyed bank is keyed by a recompute, so a bank saved before its key was assigned cannot stay unreachable.")]
public async Task ShouldKeyAnUnkeyedBankWhenRecomputing()

[Test, Description("Two banks sharing a key are split apart, because nothing connects a player's settlements yet and a shared key would let them draw on each other.")]
public async Task ShouldSplitTwoBanksThatShareAKeyWhenRecomputing()
// assert: both keys are non-empty and differ from each other

[Test, Description("A bank already in a correct network of one keeps its key, so a recompute does not churn rows it has nothing to say about.")]
public async Task ShouldLeaveACorrectlyKeyedBankAloneWhenRecomputing()

[Test, Description("A recompute run twice in a row changes nothing the second time, which is what lets a conquest recompute both players without ordering the two calls.")]
public async Task ShouldChangeNothingWhenRecomputedTwice()

[Test, Description("Another Player's banks are untouched by a recompute, because connectivity is assessed per Player and a shared key across two Players is not a fault to fix here.")]
public async Task ShouldIgnoreAnotherPlayersBanksWhenRecomputing()

[Test, Description("A Player with no settlements recomputes successfully and writes nothing, since a player mid-game may hold nothing at all.")]
public async Task ShouldSucceedWhenThePlayerHasNoBanks()

[Test, Description("The consistency check reports an unkeyed bank and a shared key, which are the only two ways a derived key with no referential integrity can go wrong under an isolated network rule.")]
public async Task ShouldReportUnkeyedAndSharedKeyFaults()
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetNetworkConnectivityServiceTests"`
Expected: FAIL — the service does not exist.

- [ ] **Step 3: Implement the service**

`AssignKeyToNewBank` sets `bank.NetworkKey = Guid.NewGuid()`.

`RecomputeForPlayerAsync` reads the player's banks tracked, ordered by `Id`, and assigns a fresh key to any bank whose key is `Guid.Empty` or whose key is held by another of that player's banks. Where a key is shared, the lowest bank id keeps it and the rest are re-keyed, so the same input always produces the same output. Logs at information level how many banks it re-keyed and for which player.

The `<remarks>` carry the two facts the implementer of a later step needs: this is the **only** writer of `NetworkKey`, and the isolated rule it computes today is a placeholder that CM-64, RS-48 and RS-49 replace with a real connected-components walk.

`FindFaultsForPlayerAsync` reads untracked and reports, without writing, the same two faults.

- [ ] **Step 4: Drop the `NetworkKey` initialiser**

`public Guid NetworkKey { get; set; }` — no `= Guid.NewGuid()`. Its `<remarks>` say that `Guid.Empty` means unkeyed and that `IAssetNetworkConnectivityService` is what keys it.

- [ ] **Step 5: Register the service**

`services.AddScoped<IAssetNetworkConnectivityService, AssetNetworkConnectivityService>();` beside the provisioning registration, with a one-line comment saying it is the only writer of `NetworkKey`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Features.Assets"`
Expected: PASS. Task 1's fixtures pass an explicit key or let the helper default one, so dropping the initialiser breaks nothing.

- [ ] **Step 7: Commit** — hand Ben the message.

---

### Task 3: The settlement read returns a network

**Files:**
- Modify: `AscendantEmpire.BackendContracts/Assets/GetAssetBanks/AssetBankHoldings.cs`
- Create: `AscendantEmpire.BackendContracts/Assets/GetAssetBanks/AssetNetworkHoldings.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Services/IAssetBankReader.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Services/AssetBankReader.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Handlers/GetSettlementAssetBankHandler.cs` → rename file to `GetSettlementAssetNetworkHandler.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Handlers/GetPlayerAssetBanksHandler.cs` (XML only)
- Modify: `AscendantEmpire.FrontendContracts/Assets/GetAssetBanks/IAssetBankCollection.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Classes/DTAssetBankCollection.cs`
- Modify: `AscendantEmpire.Api/Controllers/PlayerController.cs` (~lines 280–335)
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankReaderTests.cs`
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `AssetBank.SettlementId`, `AssetBank.NetworkKey` (Task 1); `Guid.Empty` as unkeyed (Task 2).
- Produces:

```csharp
// AssetBankHoldings: SettlementIds is replaced, NetworkKey is added
public required int SettlementId { get; init; }
public required Guid NetworkKey { get; init; }

public record AssetNetworkHoldings
{
    public required Guid NetworkKey { get; init; }
    public required int PlayerId { get; init; }
    public required int SiteAssetBankId { get; init; }
    public required IReadOnlyCollection<AssetBankHoldings> Banks { get; init; }
}

// IAssetBankReader
Task<ServiceResult<AssetNetworkHoldings>> GetForSettlementAsync(AscendantEmpireContext context, int playerId, int settlementId, CancellationToken cancelToken = default);

// Handler and request, both renamed
public record GetSettlementAssetNetworkRequest(int GameId, int PlayerId, int SettlementId) : IRequest<ServiceResult<AssetNetworkHoldings>>;

// FrontendContracts
public interface IAssetNetworkHoldings
{
    int SiteAssetBankId { get; }
    IReadOnlyCollection<IAssetBankHoldings> Banks { get; }
}
// IAssetBankHoldings: SettlementIds -> int SettlementId, plus Guid NetworkKey
```

`NetworkKey` reaches the frontend contract because the all-banks read is the only place a client can see which of a player's banks form a network; without it, `GET /player/asset-banks` is a flat list with the structure stripped out.

- [ ] **Step 1: Write the failing tests**

In `AssetBankReaderTests`, rewrite `ShouldReturnTheBanksHoldingsAndMembershipsForOneSettlement` as the network read and add the four Review Focus cases:

```csharp
[Test, Description("The settlement read hands back every bank sharing the site's NetworkKey, since that is what the settlement and everything connected to it can spend.")]
public async Task ShouldReturnTheWholeNetworkForOneSettlement()
// two banks of the same player sharing one key; assert SiteAssetBankId is the named settlement's bank,
// Banks carries both in ascending bank id, and each carries its own SettlementId

[Test, Description("A bank in a network of one comes back as a network of one, so a client has one shape to read whether or not the settlement is connected.")]
public async Task ShouldReturnANetworkOfOneForAnUnconnectedSettlement()

[Test, Description("An unkeyed bank is never grouped with another unkeyed bank, because Guid.Empty is the absence of a key rather than a network two banks share.")]
public async Task ShouldNotGroupTwoUnkeyedBanksTogether()
// two banks of one player, both Guid.Empty; assert the read returns only the site's bank

[Test, Description("Another Player's bank holding the same NetworkKey value is not in the network, since connectivity is assessed per Player and the key alone is not unique across them.")]
public async Task ShouldNotReachAnotherPlayersBankWithTheSameKey()

[Test, Description("A Settlement of another Player and a Settlement with no bank are one not-found answer, so a caller learns nothing about a Settlement that was never theirs to ask about.")]
public async Task ShouldAnswerNotFoundForAnotherPlayersSettlementAndForNoBankAlike()
```

In `AssetBankQueryHandlerTests`, `ShouldReturnTheBankTheSettlementDrawsOn` asserts `SiteAssetBankId` and a single-bank `Banks`; its `Description` is rewritten.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankReaderTests|FullyQualifiedName~AssetBankQueryHandlerTests"`
Expected: FAIL — `AssetNetworkHoldings` does not exist.

- [ ] **Step 3: Reshape the contracts**

`AssetBankHoldings`: `SettlementIds` → `SettlementId`, add `NetworkKey`. Its `<remarks>` currently explain why the memberships travel with the balances and why an empty collection is a real state — rewrite both: the settlement travels because a bank is one settlement's store, and there is no empty case any more.

`AssetNetworkHoldings` is new. Its `<remarks>` say why `SiteAssetBankId` is called out separately: RS-36's local-first debit drains the site before the rest of the network, so a client that cannot tell which bank is the site cannot show what a build order will actually spend.

Frontend contracts and `DTAssetBankCollection` follow. `IAssetBankCollection`'s `<remarks>` about two banks being two pools because their settlements cannot reach each other is now wrong — a player's banks may well be one network — so rewrite it: the list is per bank because capacity and locality are per settlement, and `NetworkKey` is what says which of them can reach each other.

- [ ] **Step 4: Rewrite the reader**

`GetForSettlementAsync` resolves the site bank with one read on `bank.SettlementId == settlementId && bank.PlayerId == playerId`, projecting `(Id, NetworkKey)`. A miss is `NotFound` with the existing single message covering both "no bank" and "not yours". It then reads the network: `bank.PlayerId == playerId && bank.NetworkKey == siteKey`, **unless the site key is `Guid.Empty`**, in which case the network is the site bank alone.

`ReadBanksAsync`'s `int? assetBankId` becomes `IReadOnlyCollection<int>? assetBankIds`. **Delete the membership read and the `MembershipRow` record** — `BankRow` carries `SettlementId` and `NetworkKey` instead, so the method falls from three queries to two. Its `<remarks>` paragraph justifying the separate membership read goes with it.

`GetForPlayerAsync` keeps its signature; each bank now reports its own settlement and key.

The interface's `<remarks>` say "Every read hands back the memberships with the balances" — rewrite to the settlement and the key.

- [ ] **Step 5: Rename the handler and update the controller**

`GetSettlementAssetBankRequest`/`Handler`/`Validator` → `GetSettlementAssetNetwork*`, file renamed to match. The validator's rules are unchanged. The handler's `<remarks>` paragraph about a bank id going stale as roads are built and pillaged is now the wrong reason — the route is keyed on the settlement because a settlement is what a caller has, and which *network* it is in is what moves.

`GetPlayerAssetBanksHandler`'s signature does not change, but its `<remarks>` — a bank per pool, two banks existing precisely because their settlements cannot reach each other — describe the model this step removes. Rewrite them the same way as `IAssetBankCollection`: a bank per settlement, with `NetworkKey` saying which of them can reach each other.

In `PlayerController`: the action becomes `GetSettlementAssetNetworkAsync` — action names are the Swagger operation ids, so the name has to say what it returns — with `[ProducesResponseType(typeof(IAssetNetworkHoldings), 200)]` and the route `asset-banks/{settlementId}` unchanged. `ToWireAssetBank` keeps its job and gains `SettlementId` and `NetworkKey`; a new `ToWireAssetNetwork(AssetNetworkHoldings)` wraps it. Fix the XML `<remarks>` and the `<response code="200">` text, which say "with its memberships and holdings".

- [ ] **Step 6: Run the tests**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Features.Assets"`
Expected: PASS.

- [ ] **Step 7: Commit** — hand Ben the message.

---

### Task 4: The drawdown's settlement arm resolves through `SettlementId`

A swap of the resolving query, nothing more. Behaviour, messages and refusals are unchanged.

**Files:**
- Modify: `AscendantEmpire.Application/Features/Assets/Services/AssetBankDrawdownService.cs` (`DebitSettlementBankAsync`, ~lines 171–185)
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankDrawdownServiceTests.cs`

**Interfaces:**
- Consumes: `AssetBank.SettlementId` (Task 1).
- Produces: nothing new. `IAssetBankDrawdownService` is untouched.

- [ ] **Step 1: Write the failing test**

```csharp
[Test, Description("A settlement expense is met from the bank keyed on that Settlement, so the bank a spend lands on is the settlement's own store rather than a membership lookup.")]
public async Task ShouldDebitTheBankKeyedOnTheSettlement()
// a bank for _SETTLEMENT_ID with no membership row at all; assert the debit succeeds and the balance falls
```

Arrange it with a bank that has `SettlementId` set and **no** membership, which is what proves the resolution moved.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankDrawdownServiceTests"`
Expected: FAIL with the existing `NotFound` — "has no asset bank belonging to Player".

- [ ] **Step 3: Swap the query**

`context.AssetBanks.AsNoTracking().Where(bank => bank.SettlementId == settlementId && bank.PlayerId == expense.PlayerId).Select(bank => (int?)bank.Id)`. The comment above it explains an INNER JOIN through the `Bank` navigation that no longer happens — replace it with why `PlayerId` is still in the predicate: naming another player's settlement must not reach their bank.

Update the method's `<remarks>`: "A bank that is short refuses the expense rather than reaching further" now points at **RS-36** for the local-first network spend, not RS-27 for transport cost. Keep the RS-27 note where it belongs — the remainder ordering, not the reaching.

- [ ] **Step 4: Run the tests**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankDrawdownServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit** — hand Ben the message.

---

### Task 5: Provisioning stages a bank and no membership

**Files:**
- Modify: `AscendantEmpire.Application/Features/Assets/Services/AssetBankProvisioningService.cs`
- Modify: `AscendantEmpire.Application/Features/Assets/Services/IAssetBankProvisioningService.cs`
- Modify: `AscendantEmpire.Application/Features/ForceOrders/Handlers/SettleForceHandler.cs` (~line 360, comment only)
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankProvisioningServiceTests.cs`
- Test: `AscendantEmpire.Tests/Features/ForceOrders/Handlers/SettleForceHandlerTests.cs` (lines 743, 769 — containerised)

**Interfaces:**
- Consumes: `IAssetNetworkConnectivityService.AssignKeyToNewBank` (Task 2).
- Produces: `ProvisionForNewSettlementAsync`'s signature is unchanged. The service now takes `IAssetNetworkConnectivityService` in its constructor alongside the logger.

- [ ] **Step 1: Write the failing tests**

In `AssetBankProvisioningServiceTests`, the four membership assertions (lines 56, 82, 112, 133) become assertions on the staged bank's `SettlementId`, and one new case:

```csharp
[Test, Description("A provisioned bank arrives keyed, because an unkeyed bank is unreachable by its own settlement until something keys it.")]
public async Task ShouldStageTheBankAlreadyKeyed()
// assert: the single AssetBank row has NetworkKey other than Guid.Empty

[Test, Description("Provisioning stages no membership row, since AssetBank.SettlementId is now what joins a bank to its Settlement.")]
public async Task ShouldStageNoMembership()
```

In `SettleForceHandlerTests`, the two membership reads become `context.AssetBanks.SingleAsync()` with a `SettlementId` assertion, and one of the two also asserts the key is set — a founding has to come out of the real pipeline keyed, not just out of the service in isolation.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankProvisioningServiceTests"`
Expected: FAIL on the new key and no-membership cases.

- [ ] **Step 3: Stage the bank alone, keyed**

Inject `IAssetNetworkConnectivityService`. Build `new AssetBank { PlayerId = playerId, Settlement = settlement }`, call `AssignKeyToNewBank(bank)`, add it, and **delete the membership staging**. The `// One bank, one member, no balances.` comment becomes one that says why the navigation rather than the id (the `Settlement` is routinely unsaved) — the existing comment on the membership already says this and moves up.

- [ ] **Step 4: Rewrite the interface's remarks**

Three paragraphs describe a model this step removes: the one about a membership being staged and joined through its navigation, the one calling a camp's attachment a **merge** of banks, and the "each will land as a method beside this one" paragraph about repointing and folding. They become: the bank is the settlement's own store for its whole life; a camp coming to share a village's stock is a shared `NetworkKey` (RS-35) rather than a merge; `NetworkKey` is assigned here through the connectivity service and never written directly. The `<returns>` note about the membership's primary key refusing a duplicate becomes the unique index.

In `SettleForceHandler`, the comment above the provisioning call mentions the membership — fix the comment; the call itself does not change.

- [ ] **Step 5: Run the in-memory tests, then the two containerised cases**

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~AssetBankProvisioningServiceTests"`
Expected: PASS.

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~SettleForceHandlerTests"`
Expected: PASS. This is tier 3 and pins port 14330 — check with Ben that no Visual Studio run is live first.

- [ ] **Step 6: Commit** — hand Ben the message.

---

### Task 6: Retire `AssetBankSettlementMembership`

Nothing reads or writes it after Task 5, so this is pure removal.

**Files:**
- Delete: `AscendantEmpire.Domain/Entities/Assets/AssetBankSettlementMembership.cs`
- Modify: `AscendantEmpire.Domain/Entities/Assets/AssetBank.cs` (drop `SettlementMemberships`)
- Modify: `AscendantEmpire.Application/Data/AscendantEmpireContext.cs` (drop the `DbSet` at ~line 294 and the configuration block at ~line 731)
- Test: `AscendantEmpire.Tests/Features/Assets/AssetBankTestBase.cs` (drop the membership write from `CreateBankAsync`)

- [ ] **Step 1: Delete the entity, the `DbSet`, the configuration block and the navigation**

The `AssetBankBalance` configuration's `WithMany(x => x.Balances)` is unaffected. Nothing else references the type.

- [ ] **Step 2: Drop the membership write from the test base**

`CreateBankAsync` keeps its signature from Task 1 — one `settlementId`, an optional `networkKey` — and now writes only the bank and its balances. Its `<remarks>` paragraph about a membership's Settlement FK not being enforced in-memory becomes one about the bank's.

- [ ] **Step 3: Build and run the asset-bank tests**

Run: `dotnet build` then `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Features.Assets"`
Expected: build clean with no references to the deleted type, and PASS. A compile error here names a reader this plan missed — fix it in the task that owns it rather than here.

- [ ] **Step 4: Commit** — hand Ben the message.

---

### Task 7: The migration, the clearing SQL, and the docs

The model is final, so one migration covers the whole change.

**Files:**
- Create: `AscendantEmpire.Application/Migrations/<timestamp>_RS-33_AssetBankPerSettlement.cs` (scaffolded)
- Modify: `AscendantEmpire.Application/Migrations/AscendantEmpireContextModelSnapshot.cs` (scaffolded)
- Modify: `docs/design/asset-banks.md`
- Modify: `docs/work-in-progress/asset-bank-refactor.md` (the Order block)
- Modify: `AscendantEmpire.Domain/Entities/Assets/AssetBankBalance.cs` (XML only)
- Modify: `AscendantEmpire.Domain/Enums/Assets/AssetStorageScope.cs` (XML only)

- [ ] **Step 1: Hand Ben the clearing SQL and wait**

The migration adds a non-nullable `SettlementId` to a table that may hold rows, which SQL Server refuses without a default, and no default is honest here. Ben clears the tables himself:

```sql
DELETE FROM assets.AssetBankBalance;
DELETE FROM assets.AssetBankSettlementMembership;
DELETE FROM assets.AssetBank;
```

Do not scaffold until he confirms he has run it. Nothing in a dev game depends on banked stock — the asset-bank pipeline has no production caller yet.

- [ ] **Step 2: Scaffold the migration**

Run: `dotnet ef migrations add RS-33_AssetBankPerSettlement --project AscendantEmpire.Application --startup-project AscendantEmpire.Api`

- [ ] **Step 3: Read the scaffolded migration and check four things**

Drops `AssetBankSettlementMembership`; adds `SettlementId` non-nullable with `FK_AssetBank_Settlements_SettlementId`; adds `UX_AssetBank_Settlement` unique and `IX_AssetBank_Player_NetworkKey`; drops `IX_AssetBank_Player`. No `DELETE` statement anywhere. Expected: all four present, nothing else.

- [ ] **Step 4: Rewrite `docs/design/asset-banks.md`**

- The model table loses the membership row; `AssetBank` is keyed on `Id` and holds one settlement's store with a `NetworkKey`.
- "One bank, many settlements" becomes one bank per settlement, networked by a shared key, carrying the reason: a pooled row has no per-settlement place for a capacity cap to bind and no way to spread a deposit, and a settlement cut off by a pillaged road kept its own stock instead of being handed a new empty bank.
- The Asset Bank Rules list: the camp-attach bullet's effect becomes a shared `NetworkKey` and a feeder designation; connect and disconnect become re-keys with no balance moved and no bank created or deleted. **Each of those states plainly that it is not built**, naming RS-35 for the camp, and CM-64, RS-48 and RS-49 for roads, rivers and coastlines.
- Both Out of Scope conquest bullets shorten to an ownership move plus a re-key of both players, pointing at RS-34. The bank-dissolution bookkeeping they describe no longer exists.
- Per the docs rule, no before-and-after notes and no "was" history.

- [ ] **Step 5: Add the four new tasks to the refactor notes' Order block**

CM-64, RS-48 and RS-49 hang off RS-33 beside RS-34/35/36/42; RS-50 joins RS-47 at the end. One line each, no restating what the tasks say.

- [ ] **Step 6: Fix the two remaining XML blocks**

`AssetBankBalance`'s composite-key `<para>` is still right; its reference to the bank as a pool is not. `AssetStorageScope.SettlementBank` says "in the bank of whichever settlement network holds it" — it is now the settlement's own bank, reachable across its network.

- [ ] **Step 7: Verify**

Run: `dotnet build`
Expected: clean.

Run: `dotnet test AscendantEmpire.Tests --filter "FullyQualifiedName~Features.Assets"`
Expected: PASS.

Then ask Ben whether to run both full suites for the commit message, per his standing rule — the targeted filter above does not cover whatever else touches these contracts.

- [ ] **Step 8: Commit** — hand Ben the message.
