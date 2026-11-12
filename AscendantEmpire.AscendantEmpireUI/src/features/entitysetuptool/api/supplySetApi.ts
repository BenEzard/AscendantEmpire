// supplySetApi — the supply set catalogue behind the Entity Setup Tool's Supply Sets dialog. A set names the Assets one of a force's needs may be met with;
// a unit component then requires a quantity of a set rather than of one Asset.

import { http } from "@/api/http";

/**
 * The need a set satisfies. Mirrors the backend SupplyType enum, which the API serialises as its member name.
 */
export type SupplyType = "Food" | "Fuel" | "Repair";

/** One Asset in a set, as the catalogue read returns it. */
export interface SupplySetAssetView {
  assetId: number;
  assetCode: string;
  assetDisplayName: string;
  /** Raw Scale-1,000 — 1,000 is one unit of the Asset per unit of the need. */
  amountPerUnit: number;
  /** How heavily this Asset is weighted when one member is drawn. Read only for a Repair set. */
  selectionWeight: number;
}

/** One supply set and its members. */
export interface SupplySetView {
  id: number;
  code: string;
  displayName: string;
  supplyType: SupplyType;
  assets: SupplySetAssetView[];
}

/** One member on the way out. Assets are named by code so a set can be authored without knowing database ids. */
export interface UpsertSupplySetAsset {
  assetCode: string;
  amountPerUnit: number;
  selectionWeight: number;
}

/** A set on the way out. Submitting it reconciles its members to exactly this list. */
export interface UpsertSupplySet {
  code: string;
  displayName: string;
  supplyType: SupplyType;
  assets: UpsertSupplySetAsset[];
}

/** GET /supply-sets — every set in the catalogue, ordered by code. */
export async function getSupplySets(signal?: AbortSignal): Promise<SupplySetView[]> {
  const { data } = await http.get<{ supplySets: SupplySetView[] }>("/supply-sets", { signal });
  return data.supplySets;
}

/** POST /supply-sets — creates a set. Its code must not already be taken. */
export async function createSupplySet(set: UpsertSupplySet): Promise<SupplySetView> {
  const { data } = await http.post<SupplySetView>("/supply-sets", set);
  return data;
}

/** PUT /supply-sets/{id} — replaces a set's fields and reconciles its members to those given. */
export async function updateSupplySet(id: number, set: UpsertSupplySet): Promise<SupplySetView> {
  const { data } = await http.put<SupplySetView>(`/supply-sets/${id}`, set);
  return data;
}
