// API service for the dev Unit Designer's global tag catalog. Thin wrappers over the shared axios
// `http` client, one per backend endpoint on UnitDesignController (GET/POST/PUT /UnitDesign/tags).

import { http } from "@/api/http";

import type { Tag } from "../types/Tag";

/** The fields the create/update endpoints accept — a Tag without its server-assigned id. */
export type TagUpsert = Omit<Tag, "id">;

/**
 * Get every tag in the global catalog. Exposed now for the (still stubbed) "Search Tag" flow; the
 * backend returns them under a `tags` envelope (GetTagsResult).
 */
export async function getTags(): Promise<Tag[]> {
  const { data } = await http.get<{ tags: Tag[] }>("/UnitDesign/tags");
  return data.tags;
}

/** Create a single tag. Returns the created tag (with its assigned id). */
export async function createTag(tag: TagUpsert): Promise<Tag> {
  const { data } = await http.post<Tag>("/UnitDesign/tags", tag);
  return data;
}

/** Update an existing tag by id. Returns the updated tag. Wired for the later Search/edit flow. */
export async function updateTag(id: number, tag: TagUpsert): Promise<Tag> {
  const { data } = await http.put<Tag>(`/UnitDesign/tags/${id}`, tag);
  return data;
}
