// API service for the Military Advisor's per-player configuration: officer rank ladders and organisation
// structures. Thin wrappers over the shared axios `http` client (which carries the game token, so the
// backend reads the game/player from it — nothing player-scoped is sent in these payloads).
//
// Enums round-trip as their string names (the API registers a JsonStringEnumConverter), so DomainType
// values like "Land" pass straight through without translation.

import { http } from "@/api/http";

import type { DomainType } from "@/features/unitdesign/types/UnitComponent";

/** One officer rank level, within a movement domain (mirrors OfficerRankView / SaveOfficerRankDto). */
export interface OfficerRank {
  movementDomain: DomainType;
  /** 1-based seniority level. */
  level: number;
  title: string;
  /** Hard soldier ceiling, or null for the top rank (no ceiling of its own). */
  maxSoldiers: number | null;
  maxAttachedSpecialists: number;
  inUse: boolean;
}

/** Get the calling player's officer rank ladders across all domains (under a `ranks` envelope). */
export async function getOfficerRanks(): Promise<OfficerRank[]> {
  const { data } = await http.get<{ ranks: OfficerRank[] }>("/military/officer-ranks");
  return data.ranks;
}

/** Bulk-save the officer rank ladders. Every domain present is replaced with the rows supplied for it. */
export async function saveOfficerRanks(ranks: OfficerRank[]): Promise<void> {
  await http.put("/military/officer-ranks", ranks);
}

/** A MilitaryOrganisationalGroup tag offered in the Organisation combo box. */
export interface OrganisationTag {
  id: number;
  code: string;
  displayName: string;
  /** The tag's movement domain, or null if it hasn't been assigned one. */
  movementDomain: DomainType | null;
}

/** Get the organisation tags — the searchable combo box feed (under a `tags` envelope). */
export async function getOrganisationTags(): Promise<OrganisationTag[]> {
  const { data } = await http.get<{ tags: OrganisationTag[] }>("/military/organisation-tags");
  return data.tags;
}

/** One tier of an organisation structure (mirrors OrganisationTierView / SaveOrganisationTierDto). */
export interface OrganisationTier {
  /** 1-based tier index, smallest first. */
  tierIndex: number;
  name: string;
  /** Tier 1: base soldier count. Tiers above: fan-out of the tier below. */
  multiplier: number;
}

/** A player's organisation hierarchy for one tag (mirrors OrganisationStructureView). */
export interface OrganisationStructure {
  tagId: number;
  tagCode: string;
  tagDisplayName: string;
  /** The tag's movement domain, or null if it has none. */
  movementDomain: DomainType | null;
  /** Maximum tiers allowed — derived from the domain's in-use rank count (max(1, ranks − 1)). */
  maxTiers: number;
  /** In-use officer rank titles for the domain, in level order (index 0 = level 1); empty if no domain. */
  rankTitles: string[];
  /** True when these tiers are the code default rather than a saved hierarchy. */
  isDefault: boolean;
  tiers: OrganisationTier[];
}

/** Get the player's structure for a tag (returns the code default, unsaved, when none is saved). */
export async function getOrganisationStructure(tagId: number): Promise<OrganisationStructure> {
  const { data } = await http.get<OrganisationStructure>(`/military/organisation-structures/${tagId}`);
  return data;
}

/** Save (create or replace) the player's structure for a tag. */
export async function saveOrganisationStructure(tagId: number, tiers: OrganisationTier[]): Promise<void> {
  await http.put(`/military/organisation-structures/${tagId}`, { tiers });
}
