import type { Domain } from "@/features/unitdesign/types/Domain";
import type { DamageType } from "./DamageType";
import type { UnitComponentType } from "./UnitComponentType";
import type { IconType } from "react-icons";

export type ComponentCost = Record<string, number>;

export type UpkeepCategory = "food" | "ammunition" | "fuel" | "repair";

export type UpkeepResources = Record<string, number>;

export type ComponentUpkeep = Partial<Record<UpkeepCategory, UpkeepResources>>;

export interface CommonComponentStats {
  mass: number;
  cost: ComponentCost;
  upkeep?: ComponentUpkeep;
}

export type WeaponDamageProfile = Record<DamageType, number>;

export interface WeaponStats {
  /** Damage this weapon deals, per damage type. */
  attack: WeaponDamageProfile;
  /** Closest distance (tiles) the weapon can engage. >1 creates an artillery-style close-up dead zone. */
  minimumRange: number;
  /** Furthest distance (tiles) the weapon can engage. Max 1 (with min 0/1) = melee; greater = ranged. */
  maximumRange: number;
  /**
   * Target domains this weapon can engage, each with a damage multiplier (1 = full effect).
   * A domain absent from this map cannot be attacked.
   */
  domainModifiers: Partial<Record<Domain, number>>;
}

export interface ArmourStats {
  /** Flat damage reduction applied to incoming damage, per damage type. */
  resistance: WeaponDamageProfile;
}

export type BaseSlotType = Exclude<UnitComponentType, "base">;

export interface BaseSlotDefinition {
  componentId: number;
  type: BaseSlotType;
  label: string;
  /**
   * Normalized offsets from the base center. Typical range: -1..1.
   * UnitDesigner converts these into pixels based on the available surface size.
   */
  offsetX: number;
  offsetY: number;
}

export type UnitComponent =
  | {
      id: number;
      name: string;
      type: "base";
      domain: Domain;
      iconUrl?: string;
      icon?: IconType;
      common: CommonComponentStats;
      properties: Record<string, number>;
      slots: BaseSlotDefinition[];
    }
  | {
      id: number;
      name: string;
      type: "weapon";
      domain: Domain;
      iconUrl?: string;
      icon?: IconType;
      /** Slot types this component can be installed into. Defaults to [type] when omitted. */
      slotTypes?: BaseSlotType[];
      /** If set, component is only valid for these base ids. */
      allowedBaseIds?: number[];
      common: CommonComponentStats;
      properties: Record<string, number>;
      weapon: WeaponStats;
    }
  | {
      id: number;
      name: string;
      type: "armour";
      domain: Domain;
      iconUrl?: string;
      icon?: IconType;
      /** Slot types this component can be installed into. Defaults to [type] when omitted. */
      slotTypes?: BaseSlotType[];
      /** If set, component is only valid for these base ids. */
      allowedBaseIds?: number[];
      common: CommonComponentStats;
      properties: Record<string, number>;
      armour: ArmourStats;
    }
  | {
      id: number;
      name: string;
      type: Exclude<UnitComponentType, "weapon" | "armour" | "base">;
      domain: Domain;
      iconUrl?: string;
      icon?: IconType;
      /** Slot types this component can be installed into. Defaults to [type] when omitted. */
      slotTypes?: BaseSlotType[];
      /** If set, component is only valid for these base ids. */
      allowedBaseIds?: number[];
      common: CommonComponentStats;
      properties: Record<string, number>;
    };

export const ALL_DAMAGE_TYPES: readonly DamageType[] = ["crush", "pierce", "burn", "slash", "poison"] as const;

export function labelForUnitComponentType(type: UnitComponentType): string {
  switch (type) {
    case "training":
      return "Training";
    default:
      return type.slice(0, 1).toUpperCase() + type.slice(1);
  }
}
