import type { DamageType } from "./types/DamageType";
import type { BaseSlotDefinition, UnitComponent, WeaponDamageProfile } from "./types/UnitComponent";

import humanSvgUrl from "@/assets/unit-design/human.svg";
import gladiusUrl from "@/assets/unit-design/gladius.svg";
import parachuteUrl from "@/assets/unit-design/parachute.svg";
import type { UnitComponentType } from "./types/UnitComponentType";

const DAMAGE_TYPES: readonly DamageType[] = ["crush", "pierce", "burn", "slash", "gas"] as const;

function profile(seed: number, scale: number): WeaponDamageProfile {
  const out = {} as WeaponDamageProfile;
  for (const dt of DAMAGE_TYPES) {
    let h = seed;
    for (let i = 0; i < dt.length; i++) h = (h * 33 + dt.charCodeAt(i)) >>> 0;
    const v = ((h % 1000) / 1000) * scale;
    out[dt] = Math.round(v * 100) / 100;
  }
  return out;
}

function common(seed: number) {
  // Small, deterministic values so the UI looks stable.
  const mass = 5 + (seed % 7) * 1.25;
  const cost = 50 + (seed % 11) * 17;
  return { mass: Math.round(mass * 100) / 100, cost };
}

function props(seed: number, keys: string[]): Record<string, number> {
  const out: Record<string, number> = {};
  for (const k of keys) {
    let h = seed;
    for (let i = 0; i < k.length; i++) h = (h * 31 + k.charCodeAt(i)) >>> 0;
    out[k] = Math.round((((h % 10_000) / 100) as number) * 100) / 100;
  }
  return out;
}

export const DUMMY_UNIT_COMPONENTS: UnitComponent[] = [
  // base
  {
    id: 1,
    name: "Demo for Land",
    type: "base",
    domain: "Land",
    common: common(1),
    properties: props(1, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 101, type: "weapon", label: "Weapon", offsetX: 1, offsetY: 0 },
      { componentId: 102, type: "armour", label: "Armour", offsetX: -1, offsetY: 0 },
      { componentId: 103, type: "engine", label: "Engine", offsetX: 0, offsetY: 1 },
      { componentId: 104, type: "equipment", label: "Equipment", offsetX: 0, offsetY: -1 },
      { componentId: 105, type: "radar", label: "Radar", offsetX: 0.75, offsetY: -0.75 },
      { componentId: 106, type: "training", label: "Training 1", offsetX: -0.75, offsetY: 0.75 },
      { componentId: 107, type: "training", label: "Training 2", offsetX: 0.75, offsetY: 0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 2,
    name: "Human",
    type: "base",
    domain: "Land",
    iconUrl: humanSvgUrl,
    common: common(2),
    properties: props(2, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 201, type: "weapon", label: "Primary Weapon", offsetX: -0.9, offsetY: 0 },
      { componentId: 202, type: "weapon", label: "Secondary Weapon", offsetX: 0.9, offsetY: 0 },
      { componentId: 203, type: "armour", label: "Helmet", offsetX: 0.4, offsetY: -2 },
      { componentId: 204, type: "armour", label: "Chest", offsetX: 0, offsetY: -1 },
      { componentId: 205, type: "engine", label: "Legs", offsetX: 0, offsetY: 1 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 3,
    name: "Siege Chassis",
    type: "base",
    domain: "Water",
    common: common(3),
    properties: props(3, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 301, type: "weapon", label: "Main Gun", offsetX: 1, offsetY: 0 },
      { componentId: 302, type: "weapon", label: "Coax Gun", offsetX: 0.75, offsetY: 0.75 },
      { componentId: 303, type: "armour", label: "Front Plating", offsetX: -1, offsetY: -0.25 },
      { componentId: 304, type: "armour", label: "Side Plating", offsetX: -1, offsetY: 0.25 },
      { componentId: 305, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 306, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 307, type: "radar", label: "Targeting", offsetX: 0.75, offsetY: -0.75 },
    ] satisfies BaseSlotDefinition[],
  },

  // weapons
  {
    id: 10,
    name: "Rivet Cannon",
    type: "weapon",
    domain: "Land",
    common: common(10),
    properties: props(10, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(10, 12),
      defense: profile(110, 5),
    },
  },
  {
    id: 11,
    name: "Arc Blaster",
    type: "weapon",
    domain: "Land",
    // Can also fit into some equipment-style slots.
    slotTypes: ["weapon", "equipment"],
    common: common(11),
    properties: props(11, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(11, 14),
      defense: profile(111, 6),
    },
  },
  {
    id: 12,
    name: "Gas Projector",
    type: "weapon",
    domain: "Air",
    common: common(12),
    properties: props(12, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(12, 10),
      defense: profile(112, 4),
    },
  },
    {
    id: 121,
    name: "Gladius",
    type: "weapon",
    domain: "Land",
    iconUrl: gladiusUrl,
    common: common(12),
    properties: props(12, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(12, 10),
      defense: profile(112, 4),
    },
  },

  // armour
  {
    id: 20,
    name: "Laminate Plating",
    type: "armour",
    domain: "Land",
    common: common(20),
    properties: props(20, ["Armor", "Mobility", "Maintenance"]),
  },
  {
    id: 21,
    name: "Reactive Panels",
    type: "armour",
    domain: "Land",
    // Example: not compatible with Human base.
    allowedBaseIds: [1, 3],
    common: common(21),
    properties: props(21, ["Armor", "Mobility", "Maintenance"]),
  },

  {
    id: 22,
    name: "Kevlar Helmet",
    type: "armour",
    domain: "Land",
    allowedBaseIds: [2],
    common: common(22),
    properties: props(22, ["Armor", "Mobility", "Maintenance"]),
  },

  // engine
  {
    id: 30,
    name: "Compact Drive",
    type: "engine",
    domain: "Land",
    common: common(30),
    properties: props(30, ["Speed", "Acceleration", "Fuel Use"]),
  },
  {
    id: 31,
    name: "Overdrive Core",
    type: "engine",
    domain: "Near Earth Orbit",
    common: common(31),
    properties: props(31, ["Speed", "Acceleration", "Fuel Use"]),
  },

  // equipment
  {
    id: 40,
    name: "Field Repair Kit",
    type: "equipment",
    domain: "Land",
    common: common(40),
    properties: props(40, ["Repair", "Cooldown", "Charges"]),
  },
  {
    id: 41,
    name: "Smoke Dispenser",
    type: "equipment",
    domain: "Land",
    common: common(41),
    properties: props(41, ["Duration", "Cooldown", "Coverage"]),
  },
  {
    id: 411,
    name: "Parachute",
    type: "equipment",
    domain: "Air",
    iconUrl: parachuteUrl,
    common: common(41),
    properties: props(41, ["Duration", "Cooldown", "Coverage"]),
  },

  // radar
  {
    id: 50,
    name: "Short-Range Scanner",
    type: "radar",
    domain: "Land",
    common: common(50),
    properties: props(50, ["Detection", "Resolution", "Stealth Penalty"]),
  },
  {
    id: 51,
    name: "Long-Range Array",
    type: "radar",
    domain: "Air",
    common: common(51),
    properties: props(51, ["Detection", "Resolution", "Stealth Penalty"]),
  },

  // training
  {
    id: 60,
    name: "Basic Drill",
    type: "training",
    domain: "Land",
    common: common(60),
    properties: props(60, ["Morale", "Discipline", "Upkeep"]),
  },
  {
    id: 61,
    name: "Advanced Doctrine",
    type: "training",
    domain: "Land",
    common: common(61),
    properties: props(61, ["Morale", "Discipline", "Upkeep"]),
  },
];

export const ALL_UNIT_COMPONENT_TYPES: readonly UnitComponentType[] = [
  "base",
  "weapon",
  "armour",
  "engine",
  "equipment",
  "radar",
  "training"
] as const;
