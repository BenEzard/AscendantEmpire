import type { BaseSlotDefinition, ComponentCost, ComponentUpkeep, UnitComponent } from "./types/UnitComponent";

import humanPrimitiveFullSizeUrl from "@/assets/unit-design/base/human-primitive_full-size.png";
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
import horseFullSizeUrl from "@/assets/unit-design/base/horse_full-size.png";
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
    iconUrl: humanPrimitiveFullSizeUrl,
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
    iconUrl: horseFullSizeUrl,
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
      attack: { crush: 2.87, pierce: 4.54, burn: 3.85, slash: 6.88, gas: 10.24 },
      defense: { crush: 1.21, pierce: 1.99, burn: 2.11, slash: 2.89, gas: 2.77 },
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
      attack: { crush: 8.85, pierce: 4.86, burn: 3.39, slash: 13.52, gas: 11.06 },
      defense: { crush: 3.82, pierce: 2.2, burn: 2.05, slash: 5.82, gas: 2.94 },
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
      attack: { crush: 0.25, pierce: 3.16, burn: 1.63, slash: 3.59, gas: 7.27 },
      defense: { crush: 0.12, pierce: 1.34, burn: 1.05, slash: 1.45, gas: 1.71 },
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
      attack: { crush: 0.25, pierce: 3.16, burn: 1.63, slash: 3.59, gas: 7.27 },
      defense: { crush: 0.12, pierce: 1.34, burn: 1.05, slash: 1.45, gas: 1.71 },
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
      attack: { crush: 2.51, pierce: 1.71, burn: 0.5, slash: 4.51, gas: 3.98 },
      defense: { crush: 0.84, pierce: 0.02, burn: 0.37, slash: 1.51, gas: 0.73 },
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
      attack: { crush: 3.65, pierce: 3.58, burn: 1.27, slash: 5.66, gas: 1.1 },
      defense: { crush: 1.23, pierce: 1.23, burn: 0.62, slash: 1.89, gas: 1.77 },
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
      attack: { crush: 0.02, pierce: 4.52, burn: 1.06, slash: 2.69, gas: 0.96 },
      defense: { crush: 0.02, pierce: 1.76, burn: 0.7, slash: 1.02, gas: 2.46 },
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
      attack: { crush: 4.74, pierce: 6.41, burn: 0.64, slash: 8.75, gas: 0.68 },
      defense: { crush: 1.6, pierce: 2.22, burn: 0.61, slash: 2.93, gas: 3.03 },
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
      attack: { crush: 7.09, pierce: 1.86, burn: 8.77, slash: 1.1, gas: 8.95 },
      defense: { crush: 2.38, pierce: 0.68, burn: 0.22, slash: 0.38, gas: 2.08 },
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
      attack: { crush: 1.45, pierce: 1.41, burn: 7.16, slash: 4.12, gas: 7.45 },
      defense: { crush: 0.55, pierce: 0.59, burn: 2.99, slash: 1.56, gas: 1.89 },
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
      attack: { crush: 6.31, pierce: 1.6, burn: 8.98, slash: 9.99, gas: 9.55 },
      defense: { crush: 2.31, pierce: 0.66, burn: 3.66, slash: 3.65, gas: 2.27 },
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
      attack: { crush: 9.67, pierce: 1.14, burn: 7.37, slash: 3.01, gas: 8.05 },
      defense: { crush: 3.88, pierce: 0.54, burn: 3.35, slash: 1.22, gas: 2.02 },
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
      attack: { crush: 0.64, pierce: 7.87, burn: 6.58, slash: 3.98, gas: 7.42 },
      defense: { crush: 1.46, pierce: 3.23, burn: 3.03, slash: 2.79, gas: 1.77 },
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
      attack: { crush: 5.48, pierce: 9.07, burn: 6.95, slash: 9.49, gas: 8.15 },
      defense: { crush: 3.79, pierce: 3.88, burn: 3.4, slash: 0.45, gas: 1.9 },
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
      attack: { crush: 11.9, pierce: 10.15, burn: 7, slash: 2.58, gas: 8.62 },
      defense: { crush: 0.9, pierce: 4.47, burn: 3.6, slash: 2.9, gas: 1.9 },
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
      attack: { crush: 3.16, pierce: 5.17, burn: 5.47, slash: 7.5, gas: 7.19 },
      defense: { crush: 3.26, pierce: 2.51, burn: 3.13, slash: 5.26, gas: 1.52 },
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
      attack: { crush: 9.54, pierce: 5.51, burn: 5.13, slash: 14.55, gas: 7.35 },
      defense: { crush: 5.62, pierce: 2.32, burn: 2.65, slash: 1.62, gas: 1.14 },
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
