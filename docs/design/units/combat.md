# Combat

Covers how a single attack is resolved between an attacker and a defender: veterancy, the profiles
fed into the calculation, and the damage/counter-attack maths. For how units move and take up
position before combat, see [unit-mechanics.md](unit-mechanics.md); for how units are organised into
formations, see [military-organisation.md](military-organisation.md), and for how a unit is designed
from components, see [unit-design-system.md](../unit-design/unit-design-system.md).

> This is a working sketch of the resolution model. Several numbers are provisional and some steps
> still have open questions called out inline.

## Veterancy

Every combatant sits at a **veterancy level**. Each level carries an `ExperienceMultiplier` and the
unit accrues `VeterancyExperiencePoints` towards the next level.

| Level | ExperienceMultiplier |
| --- | --- |
| Untrained | 0.5 |
| Regular | 1.0 |
| Hardened | 1.25 |
| Veteran | 2.0 |
| Elite | 3.0 |
| Legendary | 4.0 |

The multiplier feeds directly into the **attack value** below (the `veterancy` term). Whether the
same value also governs the *rate* at which `VeterancyExperiencePoints` are earned, or only combat
output, is an open question.

## Combatant profiles

An attack is resolved from two profiles: the attacker's `CombatantProfile` and the defender's
profile (a `CombatantProfile` plus a `DefenderProfile` overlay).

### CombatantProfile

```
CombatantProfile {
  int      PlayerId
  int?     Forced                       // set when the combatant was forced into this engagement
  int      UnitId
  (x, y)   Position
  Veterancy VeterancyLevel
  int      VeterancyExperiencePoints
  Domain   MovementDomain               // the domain the unit occupies/moves in
  float    MovementPoints
  Tag[]    Tags
  int      HealthyCount
  int      InjuredCount
  UnitDesign {
    Weapon[] Weapons {
      int      Id
      WeaponType Type
      Domain[] Domains                  // each target domain the weapon can hit, with a value
      float    MinRange
      float    MaxRange
      bool     IsSuppliedAmmo           // whether the weapon draws on an ammunition supply
      Condition WeaponCondition
    }
  }
  Biome    Biome
  Altitude Altitude
}
```

### DefenderProfile

The defender carries one extra flag on top of the `CombatantProfile`:

```
DefenderProfile {
  bool IsAmbushing
}
```

## Resolving an attack

### 1. Eligibility check

The attacker can only engage if **all** of the following hold:

- It has **at least one weapon** that can attack into the defender's **movement domain**.
- That weapon **has ammunition**, or requires none (`IsSuppliedAmmo == false`).
- Its `HealthyCount > 0`.

If no weapon qualifies, there is no attack.

### 2. Select the attack weapon

From the eligible weapons, calculate the **best weapon** the attacker can use against the defender in
the defender's domain, with `context = Attack`. This chosen weapon is referred to below as the
**AttackWeapon**.

### 3. Attack and defence values

```
AttackValue  = ((Healthy + (Injured × 0.25)) × Veterancy) × AttackWeapon

DefenceValue = sum(Armour vs AttackWeapon) × (Healthy + (Injured × 0.5))
```

- `Veterancy` is the attacker's level `ExperienceMultiplier` (see the table above).
- `AttackWeapon` is the effective value of the weapon chosen in step 2.
- `sum(Armour vs AttackWeapon)` is the defender's total armour **as it applies to the specific
  AttackWeapon** — i.e. armour is evaluated against the weapon actually being used, not a flat rating.
- Note the injured weighting differs by side: injured attackers contribute **×0.25**, injured
  defenders contribute **×0.5**.

### 4. Outright kill

If:

```
AttackValue > 1.5 × DefenceValue
```

then the defender **dies outright**.

### 5. Wounding

Otherwise, work out a **proportion of the defenders to wound**, based on the difference between the
attack and defence values. (The exact curve from "difference of values" to "number wounded" is still
to be pinned down.)

### 6. Counter-attack

If **any healthy defenders remain** after step 5, they get a counter-attack on the attacker, scaled
by the ratio of attack to defence value:

| `AttackValue / DefenceValue` | Counter-attack strength |
| --- | --- |
| ≥ 1.5 | 15% |
| ~1.3 | 30% |
| Otherwise (lower ratio) | 50% |

The weaker the attack was relative to the defence, the harder the defenders hit back.

## Open questions

- Under what circumstances (and how often) should the **defender** get a **50%-strength attack** on
  the attacker? This is the primary unresolved case in the counter-attack model.
- Does the veterancy `ExperienceMultiplier` also scale experience *gain*, or only combat output?
- What is the exact mapping from the attack/defence **difference of values** to the proportion of
  defenders wounded (step 5)?
- How does `DefenderProfile.IsAmbushing` alter the sequence above (e.g. suppressing the defender's
  free/counter attack, or granting a first strike)?

## Intentionally excluded

The following are deliberately left out at this stage — possibly permanently:

- **Discipline**
- **Morale**
