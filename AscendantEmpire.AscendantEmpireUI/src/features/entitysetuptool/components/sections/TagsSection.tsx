// TagsSection — the dialog's "Tags" page. A component references buff/classification tags by their
// code (the tag definitions themselves live in the global tag catalog, edited elsewhere). So here we
// just manage the component's set of tag *codes* as a list of chips.
//
// Rather than free-typing a code (which then has to match the catalog on save), the user picks from a
// combo box populated with the existing catalog tags — so an attached tag is always a real one. Each
// selected tag is shown as a chip coloured by its tag *type* (colour lives on the type, not the tag),
// with a tooltip of "display name - description" and a generic tag glyph.

import { useMemo } from "react";
import { HiTag, HiXMark } from "react-icons/hi2";

import { SearchableComboBox, Tooltip } from "@/components/ui";
import { isValidColor, pickTextColorOnBg } from "@/util/color";

import type { Tag, TagType } from "@/features/unitdesign/types/Tag";
import type { TagTypeInfo } from "@/features/unitdesign/types/TagType";

interface TagsSectionProps {
  /** The component's currently-attached tag codes (the committed value). */
  tagCodes: string[];
  onChange: (next: string[]) => void;
  /** The full global tag catalog to choose from. */
  availableTags: Tag[];
  /** Tag type metadata (keyed by type), used to colour a chip by its tag's type. */
  tagTypes: TagTypeInfo[];
  /** While true, the combo box shows a busy message instead of the list. */
  loading?: boolean;
  /** When set, the combo box shows this message instead of the list. */
  error?: string | null;
}

export default function TagsSection({ tagCodes, onChange, availableTags, tagTypes, loading = false, error = null }: TagsSectionProps) {
  // Fast lookups: a tag by its code (to resolve a chip's display name / description / type), and a tag
  // type's presentation metadata by its enum id (to resolve a chip's colour).
  const tagByCode = useMemo(() => {
    const map = new Map<string, Tag>();
    for (const tag of availableTags) {
      map.set(tag.code, tag);
    }
    return map;
  }, [availableTags]);

  const tagTypeById = useMemo(() => {
    const map = new Map<TagType, TagTypeInfo>();
    for (const info of tagTypes) {
      map.set(info.tagTypeId, info);
    }
    return map;
  }, [tagTypes]);

  // The combo only offers tags that aren't already attached, so a tag can't be added twice.
  const selectableTags = useMemo(() => {
    const attached = new Set(tagCodes);
    return availableTags.filter((tag) => !attached.has(tag.code));
  }, [availableTags, tagCodes]);

  const addTag = (tag: Tag) => {
    // Guard against a stale duplicate (the combo already filters these out).
    if (tagCodes.includes(tag.code)) {
      return;
    }
    onChange([...tagCodes, tag.code]);
  };

  const removeCode = (code: string) => {
    onChange(tagCodes.filter((existing) => existing !== code));
  };

  return (
    <div className="w-full max-w-xl">
      <p className="mb-2 text-xs text-slate-400">
        Pick tags to attach to this component. Only tags that exist in the catalog can be selected, so the set always resolves on save.
      </p>

      <div className="mb-3">
        <SearchableComboBox<Tag>
          items={selectableTags}
          getKey={(tag) => tag.code}
          getPrimaryText={(tag) => tag.displayName}
          getSecondaryText={(tag) => tag.code}
          getSearchText={(tag) => `${tag.displayName} ${tag.code}`}
          placeholder="Add a tag…"
          loading={loading}
          error={error}
          // Light-surface styling (the combo's default is tuned for a dark toolbar header).
          inputClassName="w-72 rounded-md border border-slate-300 px-2 py-2 text-sm text-slate-700 outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
          onSelect={addTag}
        />
      </div>

      {tagCodes.length === 0 ? (
        <p className="text-sm text-slate-500">No tags yet.</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {tagCodes.map((code) => {
            // Resolve the tag (and its type's colour) from the catalog. A code with no matching tag can
            // still occur on a legacy component; it renders as a plain chip labelled by the raw code.
            const tag = tagByCode.get(code);
            const typeInfo = tag != null ? tagTypeById.get(tag.tagType) : undefined;
            const colour = typeInfo?.colour;
            const hasColour = isValidColor(colour);

            // Tooltip is "display name - description" (description omitted when blank); the raw code when
            // the tag is unknown.
            const tooltip =
              tag != null
                ? tag.description.trim() !== ""
                  ? `${tag.displayName} - ${tag.description}`
                  : tag.displayName
                : code;

            return (
              <Tooltip key={code} content={tooltip}>
                <span
                  className={
                    hasColour
                      ? "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-sm"
                      : "inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-sm text-slate-700"
                  }
                  // When the type carries a colour, paint the chip with it and pick readable text over it.
                  style={hasColour ? { backgroundColor: colour as string, color: pickTextColorOnBg(colour) } : undefined}>
                  <HiTag size={14} aria-hidden />
                  {code}
                  <button
                    type="button"
                    // Inherit the chip's (contrast-picked) text colour so the X stays readable on any background.
                    className="opacity-70 hover:opacity-100"
                    aria-label={`Remove ${code}`}
                    onClick={() => removeCode(code)}>
                    <HiXMark size={14} aria-hidden />
                  </button>
                </span>
              </Tooltip>
            );
          })}
        </div>
      )}
    </div>
  );
}
