// API service for the dev Entity Setup Tool's global tag catalog. Thin wrappers over the shared axios
// `http` client, one per backend endpoint on UnitDesignController (GET/POST/PUT /UnitDesign/tags).

import { http } from "@/api/http";

import type { DomainType } from "../types/UnitComponent";
import type { LeadershipModel, Tag, TagType } from "../types/Tag";

/**
 * The fields the create/update endpoints accept — a Tag without its server-assigned id. Description is
 * widened to allow null so a blank description can be persisted as SQL NULL rather than an empty string.
 */
export type TagUpsert = Omit<Tag, "id" | "description"> & { description: string | null };

/** Server tag shape (GetTagsResult.Tags / TagView). Description and the organisation fields are nullable server-side. */
interface TagViewWire {
  id: number;
  code: string;
  displayName: string;
  description: string | null;
  tagType: TagType;
  movementDomain: DomainType | null;
  leadershipModel: LeadershipModel | null;
  unledBaseSize: number | null;
  unledDefaultSize: number | null;
  unledMaxSize: number | null;
  systemCreated: boolean;
}

/** Map a server TagView onto the editable Tag model (a missing description becomes an empty string). */
function mapTagViewToModel(wire: TagViewWire): Tag {
  return {
    id: wire.id,
    code: wire.code,
    displayName: wire.displayName,
    description: wire.description ?? "",
    tagType: wire.tagType,
    movementDomain: wire.movementDomain ?? null,
    leadershipModel: wire.leadershipModel ?? null,
    unledBaseSize: wire.unledBaseSize ?? null,
    unledDefaultSize: wire.unledDefaultSize ?? null,
    unledMaxSize: wire.unledMaxSize ?? null,
    systemCreated: wire.systemCreated,
  };
}

/**
 * Get every tag in the global catalog as editable Tag models. Backs the "Search Tag" drawer — each row
 * carries its id and tagType, so double-clicking opens the editor fully populated. The backend returns
 * them under a `tags` envelope (GetTagsResult).
 */
export async function getTags(): Promise<Tag[]> {
  const { data } = await http.get<{ tags: TagViewWire[] }>("/unit-design/tags");
  return data.tags.map(mapTagViewToModel);
}

/** Create a single tag. Returns the created tag (with its assigned id). */
export async function createTag(tag: TagUpsert): Promise<Tag> {
  const { data } = await http.post<Tag>("/unit-design/tags", tag);
  return data;
}

/** Update an existing tag by id. Returns the updated tag. Wired for the later Search/edit flow. */
export async function updateTag(id: number, tag: TagUpsert): Promise<Tag> {
  const { data } = await http.put<Tag>(`/unit-design/tags/${id}`, tag);
  return data;
}
