import type { TroopDisplayInfoDto } from "@/features/map/api/troopDisplayApi";

const troopImageModules = import.meta.glob("/src/assets/units/*", {
  eager: true,
  import: "default",
}) as Record<string, string>;

function normalizeTroopAssetPath(mapImagePath: string): string {
  const normalizedPath = mapImagePath.replace(/\\/g, "/").replace(/^\/+/, "").replace(/^assets\/units\/?/i, "");
  return `assets/units/${normalizedPath}`;
}

const troopImageSrcByAssetPath = new Map<string, string>(
  Object.entries(troopImageModules).map(([modulePath, resolvedSrc]) => [normalizeTroopAssetPath(modulePath.replace(/^\/src\//, "")), resolvedSrc]),
);

export function resolveTroopMapImageSrc(mapImagePath: string): string | undefined {
  return troopImageSrcByAssetPath.get(normalizeTroopAssetPath(mapImagePath));
}

export function buildTroopImageSrcByUnitTemplateId(troopDisplayInfo: TroopDisplayInfoDto[]): Map<number, string> {
  const troopImageSrcByUnitTemplateId = new Map<number, string>();

  for (const displayInfo of troopDisplayInfo) {
    const resolvedSrc = resolveTroopMapImageSrc(displayInfo.mapImagePath);
    if (!resolvedSrc) continue;
    troopImageSrcByUnitTemplateId.set(displayInfo.unitTemplateId, resolvedSrc);
  }

  return troopImageSrcByUnitTemplateId;
}