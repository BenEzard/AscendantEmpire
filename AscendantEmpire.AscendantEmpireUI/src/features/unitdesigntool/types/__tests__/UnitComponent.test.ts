import { describe, expect, it } from "vitest";

import {
  createEmptyDamageProfile,
  damageProfileToRows,
  domainModifiersToRows,
  rowsToDamageProfile,
  rowsToDomainModifiers,
  type KeyValueRow,
} from "../UnitComponent";
import type { DamageType } from "@/features/unitdesign/types/DamageType";
import type { DomainType } from "../UnitComponent";

describe("damageProfileToRows", () => {
  it("emits one row per non-zero damage type in canonical order, dropping zeros", () => {
    const rows = damageProfileToRows({ crush: 0, pierce: 1500, burn: 0, slash: 500, poison: 0 });
    // pierce comes before slash in ALL_DAMAGE_TYPES order; the zeroed types produce no row.
    expect(rows).toEqual([
      { key: "pierce", value: 1500 },
      { key: "slash", value: 500 },
    ]);
  });
});

describe("rowsToDamageProfile", () => {
  it("zero-fills every damage type, then applies the supplied rows", () => {
    const rows: KeyValueRow<DamageType>[] = [{ key: "burn", value: 2000 }];
    expect(rowsToDamageProfile(rows)).toEqual({ ...createEmptyDamageProfile(), burn: 2000 });
  });

  it("round-trips with damageProfileToRows", () => {
    const profile = { crush: 1000, pierce: 0, burn: 250, slash: 0, poison: 750 };
    expect(rowsToDamageProfile(damageProfileToRows(profile))).toEqual(profile);
  });
});

describe("domainModifiersToRows", () => {
  it("emits a row only for domains present in the record, in canonical order", () => {
    const rows = domainModifiersToRows({ Air: 1000, Land: 500 });
    // Land precedes Air in DOMAIN_TYPES order; absent domains produce no row.
    expect(rows).toEqual([
      { key: "Land", value: 500 },
      { key: "Air", value: 1000 },
    ]);
  });
});

describe("rowsToDomainModifiers", () => {
  it("builds a sparse record from the rows only (absent domains stay absent)", () => {
    const rows: KeyValueRow<DomainType>[] = [{ key: "Water", value: 1200 }];
    const modifiers = rowsToDomainModifiers(rows);
    expect(modifiers).toEqual({ Water: 1200 });
    expect("Land" in modifiers).toBe(false);
  });

  it("round-trips with domainModifiersToRows", () => {
    const modifiers = { Land: 1000, NearEarthOrbit: 300 };
    expect(rowsToDomainModifiers(domainModifiersToRows(modifiers))).toEqual(modifiers);
  });
});
