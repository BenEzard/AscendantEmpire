import type { ResourceSummary } from "@/types/map/MapResource";

/**
 * The Resource picker's options, labelled with their meta category so same-named Resources in different groups can be told apart.
 *
 * A Resource a saved row names but the list does not hold (deleted since, or the list still loading) is kept as its own option, so the select
 * never silently shows a different Resource from the one saved.
 */
export function resourceOptions(resources: ResourceSummary[], selectedIds: number[] = []): { value: number; label: string }[] {
  const options = resources.map((resource) => ({ value: resource.id, label: `${resource.displayName} (${resource.metaCategoryDisplayName})` }));
  const known = new Set(resources.map((resource) => resource.id));

  const unknown = selectedIds.filter((id) => known.has(id) === false).map((id) => ({ value: id, label: `Resource ${id}` }));

  return [...unknown, ...options];
}

/** The Resource new rows start with: the first one listed. Undefined when there are none, and rows cannot be added. */
export function firstResourceId(resources: ResourceSummary[]): number | undefined {
  return resources[0]?.id;
}
