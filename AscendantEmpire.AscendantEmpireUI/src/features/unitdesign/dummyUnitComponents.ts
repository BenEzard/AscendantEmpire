import type { DamageType } from "./types/DamageType";
import type { BaseSlotDefinition, ComponentCost, ComponentUpkeep, UnitComponent, WeaponDamageProfile } from "./types/UnitComponent";

import humanSvgUrl from "@/assets/unit-design/human.svg";
import gladiusUrl from "@/assets/unit-design/gladius.svg";
import knifeUrl from "@/assets/unit-design/knife.svg";
import parachuteUrl from "@/assets/unit-design/parachute.svg";
import type { UnitComponentType } from "./types/UnitComponentType";
import ak47Url from "@/assets/unit-design/AK47.svg";
import ballistaUrl from "@/assets/unit-design/ballista.svg";
import binocularsUrl from "@/assets/unit-design/binoculars.svg";
import bulletproofUrl from "@/assets/unit-design/bulletproof.svg";
import cannonUrl from "@/assets/unit-design/cannon.svg";
import fieldGunUrl from "@/assets/unit-design/field-gun.svg";
import fighterJetUrl from "@/assets/unit-design/fighter-jet.svg";
import fullHelmetUrl from "@/assets/unit-design/full-helmet.svg";
import gasMaskUrl from "@/assets/unit-design/gas-mask.svg";
import grenadeUrl from "@/assets/unit-design/grenade.svg";
import launcherBazookaUrl from "@/assets/unit-design/launcher-bazooka.svg";
import machineGunUrl from "@/assets/unit-design/machine-gun.svg";
import molotovUrl from "@/assets/unit-design/molotov.svg";
import plateMailUrl from "@/assets/unit-design/plate-mail.svg";
import rifleGunUrl from "@/assets/unit-design/rifle-gun.svg";
import rocketTankUrl from "@/assets/unit-design/rocket-tank.svg";
import smallTankUrl from "@/assets/unit-design/small-tank.svg";
import spearUrl from "@/assets/unit-design/spear.svg";
import submachineUrl from "@/assets/unit-design/submachine.svg";
import submarineUrl from "@/assets/unit-design/submarine.svg";
import tankUrl from "@/assets/unit-design/tank.svg";
import tank2Url from "@/assets/unit-design/tank2.svg";
import woodClubUrl from "@/assets/unit-design/wood-club.svg";

import { GiChariot, GiElephant } from "react-icons/gi";
import { LiaHorseSolid } from "react-icons/lia";

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

  // Default: everything costs Gold; some items also cost another resource.
  const gold = 50 + (seed % 11) * 17;
  const cost: ComponentCost = { Gold: gold };

  if (seed % 4 === 0) cost["Iron"] = 10 + (seed % 6) * 5;
  if (seed % 7 === 0) cost["Wood"] = 5 + (seed % 5) * 3;

  const upkeep: ComponentUpkeep = {};
  if (seed % 4 === 0) upkeep.food = { "Human Food": 1 + (seed % 3) };
  if (seed % 7 === 0) upkeep.ammunition = { "AP Rounds": 1 + (seed % 4) };
  if (seed % 3 === 0) upkeep.fuel = { Diesel: 1 + (seed % 5) };
  if (seed % 5 === 0) upkeep.repair = { Steel: 1 + (seed % 2) };

  const hasUpkeep = Object.values(upkeep).some((v) => (v ? Object.values(v).some((n) => n > 0) : false));

  return { mass: Math.round(mass * 100) / 100, cost, upkeep: hasUpkeep ? upkeep : undefined };
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
    common: { mass: common(2).mass, cost: { Human: 1, Gold: 50 }, upkeep: { food: { "Human Food": 1 } } },
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
    id: 4,
    name: "Tank",
    type: "base",
    domain: "Land",
    iconUrl: tankUrl,
    common: {
      ...common(4),
      upkeep: {
        food: { "Human Food": 2 },
        fuel: { Diesel: 5 },
        ammunition: { "AP Rounds": 10, "AT Rounds": 2 },
        repair: { Steel: 3, "Machine Parts": 1, Circuitry: 1 },
      },
    },
    properties: props(4, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 401, type: "weapon", label: "Main Gun", offsetX: 1, offsetY: 0 },
      { componentId: 402, type: "weapon", label: "Coax Gun", offsetX: 0.75, offsetY: 0.75 },
      { componentId: 403, type: "armour", label: "Front Plating", offsetX: -1, offsetY: -0.25 },
      { componentId: 404, type: "armour", label: "Side Plating", offsetX: -1, offsetY: 0.25 },
      { componentId: 405, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 406, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 407, type: "radar", label: "Targeting", offsetX: 0.75, offsetY: -0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 5,
    name: "Horse",
    type: "base",
    domain: "Land",
    icon: LiaHorseSolid,
    common: {
      ...common(5),
      upkeep: {
        food: { "Human Food": 1 },
        fuel: { Fodder: 2 },
        repair: { Medicine: 1 },
      },
    },
    properties: props(5, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 501, type: "weapon", label: "Primary Weapon", offsetX: -0.9, offsetY: 0 },
      { componentId: 502, type: "weapon", label: "Secondary Weapon", offsetX: 0.9, offsetY: 0 },
      { componentId: 503, type: "armour", label: "Barding", offsetX: 0, offsetY: -1 },
      { componentId: 504, type: "engine", label: "Legs", offsetX: 0, offsetY: 1 },
      { componentId: 505, type: "equipment", label: "Saddlebags", offsetX: 0.6, offsetY: -0.6 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 6,
    name: "Chariot",
    type: "base",
    domain: "Land",
    icon: GiChariot,
    common: common(6),
    properties: props(6, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 601, type: "weapon", label: "Primary Weapon", offsetX: -0.9, offsetY: 0 },
      { componentId: 602, type: "weapon", label: "Secondary Weapon", offsetX: 0.9, offsetY: 0 },
      { componentId: 603, type: "armour", label: "Plating", offsetX: 0, offsetY: -1 },
      { componentId: 604, type: "engine", label: "Harness", offsetX: 0, offsetY: 1 },
      { componentId: 605, type: "equipment", label: "Storage", offsetX: 0.6, offsetY: -0.6 },
      { componentId: 606, type: "training", label: "Training", offsetX: -0.6, offsetY: -0.6 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 7,
    name: "Elephant",
    type: "base",
    domain: "Land",
    icon: GiElephant,
    common: common(7),
    properties: props(7, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 701, type: "weapon", label: "Howdah Weapon", offsetX: 0.9, offsetY: 0 },
      { componentId: 702, type: "armour", label: "Hide", offsetX: 0, offsetY: -1 },
      { componentId: 703, type: "engine", label: "Legs", offsetX: 0, offsetY: 1 },
      { componentId: 704, type: "equipment", label: "Gear", offsetX: -0.6, offsetY: -0.6 },
      { componentId: 705, type: "radar", label: "Spotter", offsetX: 0.6, offsetY: -0.6 },
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

  // base (asset-backed)
  {
    id: 8,
    name: "Small Tank",
    type: "base",
    domain: "Land",
    iconUrl: smallTankUrl,
    common: common(8),
    properties: props(8, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 801, type: "weapon", label: "Main Gun", offsetX: 1, offsetY: 0 },
      { componentId: 802, type: "weapon", label: "Aux Weapon", offsetX: 0.75, offsetY: 0.75 },
      { componentId: 803, type: "armour", label: "Plating", offsetX: -1, offsetY: 0 },
      { componentId: 804, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 805, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 806, type: "radar", label: "Sensors", offsetX: 0.75, offsetY: -0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 9,
    name: "Rocket Tank",
    type: "base",
    domain: "Land",
    iconUrl: rocketTankUrl,
    common: common(9),
    properties: props(9, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 901, type: "weapon", label: "Launcher", offsetX: 1, offsetY: 0 },
      { componentId: 902, type: "armour", label: "Plating", offsetX: -1, offsetY: 0 },
      { componentId: 903, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 904, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 905, type: "radar", label: "Sensors", offsetX: 0.75, offsetY: -0.75 },
      { componentId: 906, type: "training", label: "Training", offsetX: -0.75, offsetY: 0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 14,
    name: "Fighter Jet",
    type: "base",
    domain: "Air",
    iconUrl: fighterJetUrl,
    common: common(14),
    properties: props(14, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 1401, type: "weapon", label: "Hardpoint L", offsetX: -0.9, offsetY: 0 },
      { componentId: 1402, type: "weapon", label: "Hardpoint R", offsetX: 0.9, offsetY: 0 },
      { componentId: 1403, type: "engine", label: "Engine", offsetX: 0, offsetY: 1 },
      { componentId: 1404, type: "equipment", label: "Pod", offsetX: 0, offsetY: -1 },
      { componentId: 1405, type: "radar", label: "Radar", offsetX: 0.75, offsetY: -0.75 },
      { componentId: 1406, type: "training", label: "Training", offsetX: -0.75, offsetY: 0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 15,
    name: "Submarine",
    type: "base",
    domain: "Water",
    iconUrl: submarineUrl,
    common: common(15),
    properties: props(15, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 1501, type: "weapon", label: "Torpedoes", offsetX: 1, offsetY: 0 },
      { componentId: 1502, type: "armour", label: "Hull", offsetX: -1, offsetY: 0 },
      { componentId: 1503, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 1504, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 1505, type: "radar", label: "Sonar", offsetX: 0.75, offsetY: -0.75 },
      { componentId: 1506, type: "training", label: "Training", offsetX: -0.75, offsetY: 0.75 },
    ] satisfies BaseSlotDefinition[],
  },
  {
    id: 16,
    name: "Tank Mk II",
    type: "base",
    domain: "Land",
    iconUrl: tank2Url,
    common: common(16),
    properties: props(16, ["Durability", "Cargo", "Crew", "Signature"]),
    slots: [
      { componentId: 1601, type: "weapon", label: "Main Gun", offsetX: 1, offsetY: 0 },
      { componentId: 1602, type: "weapon", label: "Coax", offsetX: 0.75, offsetY: 0.75 },
      { componentId: 1603, type: "armour", label: "Front Plating", offsetX: -1, offsetY: -0.25 },
      { componentId: 1604, type: "armour", label: "Side Plating", offsetX: -1, offsetY: 0.25 },
      { componentId: 1605, type: "engine", label: "Drive", offsetX: 0, offsetY: 1 },
      { componentId: 1606, type: "equipment", label: "Utility", offsetX: 0, offsetY: -1 },
      { componentId: 1607, type: "radar", label: "Targeting", offsetX: 0.75, offsetY: -0.75 },
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
  {
    id: 13,
    name: "Knife",
    type: "weapon",
    domain: "Land",
    iconUrl: knifeUrl,
    common: common(13),
    properties: props(13, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(13, 6),
      defense: profile(113, 2),
    },
  },

  // weapons (asset-backed)
  {
    id: 100,
    name: "Wood Club",
    type: "weapon",
    domain: "Land",
    iconUrl: woodClubUrl,
    common: common(100),
    properties: props(100, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(100, 6),
      defense: profile(200, 2),
    },
  },
  {
    id: 101,
    name: "Spear",
    type: "weapon",
    domain: "Land",
    iconUrl: spearUrl,
    common: common(101),
    properties: props(101, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(101, 8),
      defense: profile(201, 3),
    },
  },
  {
    id: 102,
    name: "Ballista",
    type: "weapon",
    domain: "Land",
    iconUrl: ballistaUrl,
    common: common(102),
    properties: props(102, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(102, 12),
      defense: profile(202, 4),
    },
  },
  {
    id: 103,
    name: "Grenade",
    type: "weapon",
    domain: "Land",
    iconUrl: grenadeUrl,
    common: common(103),
    properties: props(103, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(103, 9),
      defense: profile(203, 3),
    },
  },
  {
    id: 104,
    name: "Molotov",
    type: "weapon",
    domain: "Land",
    iconUrl: molotovUrl,
    common: common(104),
    properties: props(104, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(104, 8),
      defense: profile(204, 3),
    },
  },
  {
    id: 105,
    name: "AK-47",
    type: "weapon",
    domain: "Land",
    iconUrl: ak47Url,
    common: common(105),
    properties: props(105, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(105, 11),
      defense: profile(205, 4),
    },
  },
  {
    id: 106,
    name: "Rifle",
    type: "weapon",
    domain: "Land",
    iconUrl: rifleGunUrl,
    common: common(106),
    properties: props(106, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(106, 10),
      defense: profile(206, 4),
    },
  },
  {
    id: 107,
    name: "Submachine Gun",
    type: "weapon",
    domain: "Land",
    iconUrl: submachineUrl,
    common: common(107),
    properties: props(107, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(107, 10),
      defense: profile(207, 4),
    },
  },
  {
    id: 108,
    name: "Machine Gun",
    type: "weapon",
    domain: "Land",
    iconUrl: machineGunUrl,
    common: common(108),
    properties: props(108, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(108, 12),
      defense: profile(208, 5),
    },
  },
  {
    id: 109,
    name: "Cannon",
    type: "weapon",
    domain: "Land",
    iconUrl: cannonUrl,
    common: common(109),
    properties: props(109, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(109, 14),
      defense: profile(209, 6),
    },
  },
  {
    id: 110,
    name: "Field Gun",
    type: "weapon",
    domain: "Land",
    iconUrl: fieldGunUrl,
    common: common(110),
    properties: props(110, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(110, 13),
      defense: profile(210, 6),
    },
  },
  {
    id: 111,
    name: "Bazooka Launcher",
    type: "weapon",
    domain: "Land",
    iconUrl: launcherBazookaUrl,
    common: common(111),
    properties: props(111, ["Range", "Rate of Fire", "Accuracy", "Heat"]),
    weapon: {
      attack: profile(111, 15),
      defense: profile(211, 6),
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
    iconUrl: fullHelmetUrl,
    common: common(22),
    properties: props(22, ["Armor", "Mobility", "Maintenance"]),
  },

  {
    id: 23,
    name: "Bulletproof Vest",
    type: "armour",
    domain: "Land",
    iconUrl: bulletproofUrl,
    common: common(23),
    properties: props(23, ["Armor", "Mobility", "Maintenance"]),
  },
  {
    id: 24,
    name: "Plate Mail",
    type: "armour",
    domain: "Land",
    iconUrl: plateMailUrl,
    common: common(24),
    properties: props(24, ["Armor", "Mobility", "Maintenance"]),
  },
  {
    id: 25,
    name: "Gas Mask",
    type: "armour",
    domain: "Land",
    iconUrl: gasMaskUrl,
    common: common(25),
    properties: props(25, ["Armor", "Mobility", "Maintenance"]),
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
    id: 52,
    name: "Binoculars",
    type: "radar",
    domain: "Land",
    iconUrl: binocularsUrl,
    common: common(52),
    properties: props(52, ["Detection", "Resolution", "Stealth Penalty"]),
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
