// Maps each officer rank to a static react-icons component, so the Military Advisor's tables can show a
// rank as an insignia. A rank's identity is its movement domain plus its level: titles are player-editable
// (see the Officer Class page), so the icon deliberately keys off domain + level instead. The art is fixed
// game data and never varies per player, so it lives here rather than in the data model or the API.
// Mirrors the domainIcons pattern in the neighbouring domainIcons.tsx.

import type { IconType } from "react-icons";
import {
  GiAlliedStar,
  GiAnchor,
  GiAngelWings,
  GiCaptainHatProfile,
  GiCommercialAirplane,
  GiCompass,
  GiCrenelCrown,
  GiCurlyWing,
  GiDivingHelmet,
  GiEagleEmblem,
  GiJetFighter,
  GiLighthouse,
  GiRank1,
  GiRank2,
  GiRank3,
  GiSailboat,
  GiSeaDragon,
  GiSharkFin,
  GiShipWheel,
  GiSpikedShell,
  GiStarsStack,
  GiStarSwirl,
  GiSubmarine,
  GiTrident,
} from "react-icons/gi";

import type { DomainType } from "@/features/unitdesign/types/UnitComponent";

// The Land ladder, low to high. Pulled out on its own so the domains with no art of their own can share it
// (see NearEarthOrbit below, and the null-domain fallback in rankIcon).
const LAND_RANK_ICONS: readonly IconType[] = [GiRank1, GiRank2, GiRank3, GiCrenelCrown, GiAlliedStar, GiStarsStack];

/**
 * One insignia per rank level, indexed by level - 1, for every movement domain.
 *
 * <remarks>
 * <para>
 * Each array holds six entries — one per rank level — matching PlayerOfficerRank.MAX_LEVEL on the backend
 * and OFFICER_ROW_COUNT in the Officer Class table. If that ceiling ever moves, these arrays move with it.
 * </para>
 * <para>
 * The sets are deliberately distinct per domain rather than one shared chevron progression, so a naval
 * Ensign and a Land Lieutenant do not read identically. Keyed over DomainType so the compiler flags any
 * domain that is added or removed.
 * </para>
 * </remarks>
 */
export const rankIcons: Record<DomainType, readonly IconType[]> = {
  Land: LAND_RANK_ICONS,
  Water: [GiAnchor, GiSailboat, GiCompass, GiShipWheel, GiLighthouse, GiCaptainHatProfile],
  BelowWater: [GiSharkFin, GiSubmarine, GiDivingHelmet, GiSpikedShell, GiSeaDragon, GiTrident],
  Air: [GiCurlyWing, GiAngelWings, GiCommercialAirplane, GiJetFighter, GiStarSwirl, GiEagleEmblem],
  // NearEarthOrbit has no insignia set of its own yet, so it borrows the Land ladder — the same placeholder
  // stance the backend takes (it is excluded from MilitaryOrganisationDefaults.SeededDomains and falls back
  // to the Land rank titles).
  NearEarthOrbit: LAND_RANK_ICONS,
};

// The two metals the insignia are drawn in. Gold for the surface domains, silver for the ones that fly or
// dive. Both are picked to stay legible against the dialog's white/slate background — a true silver (#c0c0c0)
// all but disappears on white, so the silver here is a darker pewter that still reads as the other metal.
const GOLD_CLASS = "text-amber-500";
const SILVER_CLASS = "text-zinc-500";

/**
 * The colour each domain's insignia is drawn in, as a Tailwind text-colour class.
 *
 * <remarks>
 * <para>
 * Like the icons themselves this is static game art, not player data. Keyed over DomainType so a domain can't
 * be added without being given a metal.
 * </para>
 * </remarks>
 */
export const rankIconColours: Record<DomainType, string> = {
  Land: GOLD_CLASS,
  Water: GOLD_CLASS,
  BelowWater: SILVER_CLASS,
  Air: SILVER_CLASS,
  // NearEarthOrbit borrows the Land ladder (see above), so it borrows Land's metal too.
  NearEarthOrbit: GOLD_CLASS,
};

/**
 * The Tailwind text-colour class for a domain's insignia.
 *
 * <remarks>
 * <para>
 * A null domain falls back to the Land colour, matching the ladder fallback in <see cref="rankIcon"/> so the
 * icon and its colour always come from the same domain.
 * </para>
 * </remarks>
 */
export function rankIconColourClass(domain: DomainType | null): string {
  return domain === null ? GOLD_CLASS : rankIconColours[domain];
}

/**
 * The insignia for a rank, or undefined when there is none to draw.
 *
 * <remarks>
 * <para>
 * A null domain (an organisation tag that has not been given a movement domain) falls back to the Land
 * ladder, mirroring the backend's TitlesForDomain default. A level outside the ladder returns undefined so
 * the caller simply renders no icon rather than crashing.
 * </para>
 * </remarks>
 */
export function rankIcon(domain: DomainType | null, level: number): IconType | undefined {
  const icons = domain === null ? LAND_RANK_ICONS : rankIcons[domain];
  return icons[level - 1];
}
