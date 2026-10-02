import type { AssetSummary } from "@/types/map/MapResource";

/**
 * The Asset picker's options, labelled with their meta category so same-named Assets in different groups can be told apart.
 *
 * An Asset a saved row names but the list does not hold (deleted since, or the list still loading) is kept as its own option, so the select
 * never silently shows a different Asset from the one saved.
 */
export function assetOptions(assets: AssetSummary[], selectedIds: number[] = []): { value: number; label: string }[] {
  const options = assets.map((asset) => ({ value: asset.id, label: `${asset.displayName} (${asset.metaCategoryDisplayName})` }));
  const known = new Set(assets.map((asset) => asset.id));

  const unknown = selectedIds.filter((id) => known.has(id) === false).map((id) => ({ value: id, label: `Resource ${id}` }));

  return [...unknown, ...options];
}

/** The Asset new rows start with: the first one listed. Undefined when there are none, and rows cannot be added. */
export function firstAssetId(assets: AssetSummary[]): number | undefined {
  return assets[0]?.id;
}
