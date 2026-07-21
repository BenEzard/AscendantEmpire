// Maps each DomainType to a react-icons component, so game-aware UI (e.g. DomainSelection) can render
// a domain as an icon. The domain→icon relationship lives here rather than in the data model, since
// DomainType is a plain enum with no icon field. Mirrors the componentTypeIcons pattern in
// features/entitysetuptool/components/componentTypeIcons.tsx.

import type { IconType } from "react-icons";
import { GiCommercialAirplane, GiHills, GiSatelliteCommunication, GiSubmarine, GiWaves } from "react-icons/gi";

import type { DomainType } from "@/features/unitdesign/types/UnitComponent";

// One icon per domain. Keyed over DomainType so the compiler flags any missing/added domain.
export const domainIcons: Record<DomainType, IconType> = {
  Land: GiHills,
  Water: GiWaves,
  BelowWater: GiSubmarine,
  Air: GiCommercialAirplane,
  NearEarthOrbit: GiSatelliteCommunication,
};
