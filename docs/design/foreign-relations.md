# Foreign Relations

For every other empire, an empire holds a **relationship** record.

## Relationship attributes

| Group | Attribute | Description |
| --- | --- | --- |
| **Attitude** | Chosen Disposition | Your *chosen* attitude toward another empire. Can differ from the Public Disposition. One of: `GOOD_ADMIRATION`, `GOOD_WARM`, `GOOD_POSITIVE`, `NEUTRAL`, `BAD_ANNOYED`, `BAD_ANGER`, `BAD_HATE`. |
| | Public Disposition | Your populace's attitude toward another empire, derived from the Sentiment Score. Can differ from your Chosen Disposition. Same option set as above. |
| | Sentiment Score | A positive/negative value for how your empire feels toward the other, based on a history of interactions. Every interaction is affected by the Disposition Multiplier. |
| **Combat** | When conquering civilians / specialists / soldiers | What to do with each on conquest: `LEAVE_UNHARMED`, `MAKE_SLAVES`, `MAKE_PRISONERS_OF_WAR`, `EXECUTE`. |
| | Attempt Capture Percent | 0–100: how hard a unit tries to capture individuals. Raiding a settlement yields **slaves**; conquering a military unit yields slaves or POWs. Higher values capture more prisoners but carry more risk (the capturing unit can take damage). `0` means no slaves/POWs are taken — enemies are killed, and so fight harder when death is the only alternative. |
| **Social Status** | Slavery Ends After | Turns after which a slave becomes free and is accepted into the general populace. |
| | Slave Treatment | How harshly this empire's slaves are treated. Harsher → consume less, produce more, and die faster. |
| | Prisoner of War Treatment | As above, for POWs. |
| | Accept As Refugees? | Whether the empire accepts refugees from this empire. |
| **Trade Policy** | (shorthand) | Filters out annoying AI requests you won't agree to. |
| | Trade Embargos | Assets you won't trade under any circumstances. |
| | Open to… | Y/N flags: Map Trade, Open Borders, POW swaps, Slave swaps, Technology Trading, Land Sale, Defensive Pact, non-Strategic Asset Trading, Strategic Asset Trading. |

## Diplomatic arrangements

Agreements can be set to **auto-renew** unless the player intervenes. A message is sent **2 turns
before** an agreement renews or ends. Letting another empire know an agreement won't be renewed
*before* it ends is considered better behaviour — there is a **diplomatic penalty** for not advising
them earlier.

An agreement is held between two empires, with its own lifetime and terms, and the tiles it covers
are attached to it — a Defined passage is one agreement over a set of tiles, not terms copied onto
each one. Agreements are also **scoped to a Settlement** where the arrangement concerns its
territory, as a harvesting agreement does.

A Force present under **any** organised agreement is there lawfully, so it does **not** count as
occupying the tile and starts no occupation timer against its host. Without that, granting passage
or harvesting rights would cost the host their own land. See
[tile-ownership.md](campaign-map/tile-ownership.md#occupation).

### Support agreements

Let a guest empire's [unit support](units/unit-support-and-supply.md) needs be met in a host empire's
territory without a supply chain.

| Name | Function |
| --- | --- |
| `[Logistical \| Shared \| Full]` `[Land \| Sea \| Air \| All]` Support Agreement | **Logistical** — guest pays full support cost. **Shared** — host contributes 50% of required food. **Full** — host contributes 100% of required food. Agreement variables (not applicable to Logistical): `FUEL_SUPPLIED` (requires host to have the appropriate refuelling tech), `MEDICAL_SUPPLIED` (guest units heal as in home territory), `MUNITION_SUPPLIED`. |
| `[Land \| Sea \| Air \| All]` Support Agreement Embargo | An agreement that a target empire should **not** be permitted a Support Agreement with a signatory empire. |

### Passage agreements

Permit (or restrict) movement of an empire through defined territory.

| Name | Function |
| --- | --- |
| Permit `[Defined \| Unrestricted \| Constrained]` `[Land \| Sea \| Air \| All]` Passage Agreement `[Non-Military \| Military \| All]` | Lets a guest traverse a host's territory without triggering aggression. (Without a Support Agreement, a supply chain is still required.) **Defined** — via a host-defined passage. **Unrestricted** — go anywhere. **Constrained** — go anywhere but no closer than `SETTLEMENT_BUFFER_ZONE` to a settlement. Variables: agreement lasts `NUMBER_OF_TURNS`; may charge `COST_PER_PASSAGE` (formula based on unit size — a canal-type fee); `CAN_BUILD_TRANSPORTATION` lets the guest build transport infrastructure (but not destroy existing); `SETTLEMENT_BUFFER_ZONE`. |
| `[Land \| Sea \| Air \| All]` Passage Agreement Embargo `[Non-Military \| Military \| All]` | An agreement that a target empire should not be permitted a Passage Agreement with a signatory empire. |

### Embassies

| Name | Function |
| --- | --- |
| Diplomatic Recognition | Lets a guest build embassies in host settlements. Embassies increase trade value, promote goodwill and tech transfer (diffusion) for both, and increase the likelihood of successful espionage against the host. Variable: `MAXIMUM_EMBASSIES`. |

### Other arrangements

| Name | Function |
| --- | --- |
| `[MapResource]` Harvesting Agreement | Lets a guest harvest a MapResource in the host's territory and build related infrastructure (without destroying existing). The Assets it yields go to the guest. Variables: `GOLD_PER_TURN`, `PERCENTAGE_HARVEST` (the rest goes to the host). |
| Land Sale Agreement | Sell controlled land from one empire to another. |
| Establish `[Land \| Sea \| Air]` Base | Establish a military base on a designated tile. |
| Intermarriage | Affects the relationship between the two empires. |
