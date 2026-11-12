// assetApi — the Asset catalogue as the Entity Setup Tool's pickers need it. Shared by the Supply Sets dialog, which chooses a set's members, and the
// component dialog's ammunition editor, which chooses what a weapon fires.
//
// Owned here rather than by supplySetApi, which held it while supply sets were its only caller: a plain catalogue read belongs to neither of the two
// features that pick from it.

import { http } from "@/api/http";

/**
 * An Asset as a picker needs it.
 */
/* Trimmed from the /assets read rather than reusing the map gallery's AssetSummary, which carries the
   naturally-occurring and meta-category fields a picker has no use for. */
export interface AssetOption {
  id: number;
  code: string;
  displayName: string;
  /** Root-relative URL of the catalogue image, or "" where the Asset has none. The picker draws a generic glyph for those. */
  iconPath: string;
}

/** GET /assets — the Assets a picker can draw on. */
export async function getAssetOptions(signal?: AbortSignal): Promise<AssetOption[]> {
  const { data } = await http.get<{ assets: AssetOption[] }>("/assets", { signal });
  return data.assets;
}
