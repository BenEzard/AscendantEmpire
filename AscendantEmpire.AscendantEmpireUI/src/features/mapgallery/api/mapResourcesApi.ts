import type { MapResourceCategory, MapResourceType, ResourceSummary } from "../../../types/map/MapResource";
import { http } from "../../../api/http";

/** The whole MapResource catalogue: categories in order, each with its types in order. */
export async function getMapResourceCatalogue(): Promise<MapResourceCategory[]> {
  const { data } = await http.get<{ categories: MapResourceCategory[] }>("/map-resources/catalogue");
  return data.categories;
}

/** The body PUT /map-resources/catalogue takes: categories and types, each added or updated by code. */
export type MapResourceCataloguePayload = {
  categories: { code: string; displayName: string; sortOrder: number }[];
  types: MapResourceType[];
};

/** Adds or updates categories and types by code, in one transaction. Nothing absent is deleted. */
export async function putMapResourceCatalogue(payload: MapResourceCataloguePayload): Promise<void> {
  await http.put("/map-resources/catalogue", payload, {
    headers: { "Content-Type": "application/json" },
  });
}

/** Deletes a type that no map or game uses. */
export async function deleteMapResourceType(code: string): Promise<void> {
  await http.delete(`/map-resources/types/${encodeURIComponent(code)}`);
}

/** Deletes a category with no types in it. */
export async function deleteMapResourceCategory(code: string): Promise<void> {
  await http.delete(`/map-resources/categories/${encodeURIComponent(code)}`);
}

/** Every Resource, for the pickers. */
export async function getResources(): Promise<ResourceSummary[]> {
  const { data } = await http.get<{ resources: ResourceSummary[] }>("/resources");
  return data.resources;
}
