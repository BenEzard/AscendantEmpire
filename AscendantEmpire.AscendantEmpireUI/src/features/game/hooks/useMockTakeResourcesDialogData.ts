import { useMemo } from "react";
import type { ObservedForceTile } from "@/features/map/components/TileTypes";
import type { AssetStack, TakeResourcesForce, TileAssetStack } from "../components/takeResourcesCargo";

// Stand-in for the real options read until the backend is rewired (F-40). Everything below is placeholder data.

const MOCK_CAPACITY_PER_HEALTHY_INDIVIDUAL = 5;

const BERRIES: AssetStack = { assetId: 9001, displayName: "Berries", iconPath: "/resources/food/berries_40px.png", quantity: 0 };
const FISH: AssetStack = { assetId: 9002, displayName: "Fish", iconPath: "/resources/food/fish_40px.png", quantity: 0 };
const MEAT: AssetStack = { assetId: 9003, displayName: "Meat", iconPath: "/resources/food/meat-02_40px.png", quantity: 0 };
const ROCKS: AssetStack = { assetId: 9004, displayName: "Rocks", iconPath: "/resources/stone/generic-rock-pile_40px.png", quantity: 0 };

const MOCK_TILE_ASSETS: readonly TileAssetStack[] = [
  { ...BERRIES, quantity: 7000, isEstimate: true },
  { ...FISH, quantity: 340, isEstimate: false },
  { ...ROCKS, quantity: 120, isEstimate: true },
];

const MOCK_CARRYING: readonly AssetStack[][] = [[{ ...MEAT, quantity: 20 }], [{ ...BERRIES, quantity: 15 }, { ...FISH, quantity: 5 }]];

function turnRemaining(force: ObservedForceTile): number {
  if (force.campaignMovementRemaining == null || force.campaignMovementPoints == null || force.campaignMovementPoints <= 0) {
    return force.hasCampaignMovementRemaining ? 1 : 0;
  }

  return Math.min(1, Math.max(0, force.campaignMovementRemaining / force.campaignMovementPoints));
}

export function useMockTakeResourcesDialogData(
  selectedForces: readonly ObservedForceTile[],
  imageSrcByUnitTemplateId: ReadonlyMap<number, string> | undefined,
  nameByUnitTemplateId: ReadonlyMap<number, string> | undefined,
): { tileAssets: readonly TileAssetStack[]; forces: readonly TakeResourcesForce[] } {
  const forces = useMemo(
    () =>
      selectedForces.map<TakeResourcesForce>((force, index) => ({
        forceId: force.id,
        displayName: nameByUnitTemplateId?.get(force.unitTemplateId) ?? `Force #${force.id}`,
        imageSrc: imageSrcByUnitTemplateId?.get(force.unitTemplateId),
        healthyIndividuals: force.healthyTroops,
        injuredIndividuals: force.injuredTroops,
        initialStrength: force.initialStrength,
        movementRemaining: force.campaignMovementRemaining ?? 0,
        movementPoints: force.campaignMovementPoints ?? 0,
        turnRemaining: turnRemaining(force),
        capacity: Math.max(0, force.healthyTroops) * MOCK_CAPACITY_PER_HEALTHY_INDIVIDUAL,
        carrying: (MOCK_CARRYING[index] ?? []).map((stack) => ({ ...stack })),
      })),
    [selectedForces, imageSrcByUnitTemplateId, nameByUnitTemplateId],
  );

  return { tileAssets: MOCK_TILE_ASSETS, forces };
}
