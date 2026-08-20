// API service for the dev Entity Setup Tool's tag type catalog. Thin wrappers over the shared axios
// `http` client, one per backend tag-type endpoint (GET/PUT /unit-design-tags/types).

import { http } from "@/api/http";

import type { TagType } from "../types/Tag";
import type { TagTypeInfo } from "../types/TagType";

/** Server tag-type shape (GetTagTypesResult.TagTypes / TagTypeView). */
interface TagTypeViewWire {
  tagTypeId: TagType;
  displayName: string;
  description: string;
  isInherited: boolean;
  colour: string | null;
  icon: string | null;
}

/** Map a server TagTypeView onto the editable TagTypeInfo model. */
function mapTagTypeViewToModel(wire: TagTypeViewWire): TagTypeInfo {
  return {
    tagTypeId: wire.tagTypeId,
    displayName: wire.displayName,
    description: wire.description,
    isInherited: wire.isInherited,
    colour: wire.colour,
    icon: wire.icon,
  };
}

/**
 * Get every tag type's editable metadata. Backs the Tag Type Setup dialog's selection box and the Tag
 * Setup dialog's type dropdown. The backend returns them under a `tagTypes` envelope (GetTagTypesResult).
 */
export async function getTagTypes(): Promise<TagTypeInfo[]> {
  const { data } = await http.get<{ tagTypes: TagTypeViewWire[] }>("/unit-design-tags/types");
  return data.tagTypes.map(mapTagTypeViewToModel);
}

/**
 * Bulk-save the whole tag type set. The backend upserts every row (create missing, update existing),
 * so the caller sends the entire edited collection. Returns nothing (the endpoint responds 204).
 */
export async function saveTagTypes(tagTypes: TagTypeInfo[]): Promise<void> {
  await http.put("/unit-design-tags/types", tagTypes);
}
