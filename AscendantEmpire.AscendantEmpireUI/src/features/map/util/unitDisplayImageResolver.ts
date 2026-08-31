import type { UnitDisplayInfoDto } from "@/features/map/api/unitDisplayApi";

const unitImageModules = import.meta.glob("/src/assets/units/*", {
  eager: true,
  import: "default",
}) as Record<string, string>;

function normalizeUnitAssetPath(mapImagePath: string): string {
  const normalizedPath = mapImagePath.replace(/\\/g, "/").replace(/^\/+/, "").replace(/^assets\/units\/?/i, "");
  return `assets/units/${normalizedPath}`;
}

const unitImageSrcByAssetPath = new Map<string, string>(
  Object.entries(unitImageModules).map(([modulePath, resolvedSrc]) => [normalizeUnitAssetPath(modulePath.replace(/^\/src\//, "")), resolvedSrc]),
);

export function resolveUnitMapImageSrc(mapImagePath: string): string | undefined {
  return unitImageSrcByAssetPath.get(normalizeUnitAssetPath(mapImagePath));
}

export function buildUnitImageSrcByUnitTemplateId(unitDisplayInfo: UnitDisplayInfoDto[]): Map<number, string> {
  const unitImageSrcByUnitTemplateId = new Map<number, string>();

  for (const displayInfo of unitDisplayInfo) {
    const resolvedSrc = resolveUnitMapImageSrc(displayInfo.mapImagePath);
    if (!resolvedSrc) continue;
    unitImageSrcByUnitTemplateId.set(displayInfo.unitTemplateId, resolvedSrc);
  }

  return unitImageSrcByUnitTemplateId;
}