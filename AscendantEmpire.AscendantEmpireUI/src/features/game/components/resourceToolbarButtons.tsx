import type { ToolBarButton } from "@/components/ui/toolbar/Toolbar";
import type { PlayerResourceMetaCategory } from "@/features/game/api/playerResourcesApi";
import ResourceIconImage from "@/features/game/resources/ResourceIconImage";
import { resourceToolbarButtonDomId } from "@/features/game/hooks/useResourcePanels";

/** Rendered size of a meta-category's icon, matching the 1.5rem the toolbar's glyph icons come out at. An image gets no em sizing for free. */
const RESOURCE_BUTTON_ICON_SIZE = 24;

/**
 * One toolbar button per unlocked meta-category, each opening that meta-category's panel.
 *
 * Not sorted here. The API returns the meta-categories already ordered by sort order and then name, and re-sorting on sort order alone would lose
 * that tie-break.
 */
export function buildResourceToolbarButtons(
  metaCategories: PlayerResourceMetaCategory[],
  openMetaCategoryCode: string | null,
  onToggleResourcePanel: (metaCategoryCode: string) => void,
): ToolBarButton[] {
  return metaCategories.map((metaCategory) => ({
    key: metaCategory.code,
    // The panel anchors to this button, so the id has to be the one the panel hook derives from the same code.
    domId: resourceToolbarButtonDomId(metaCategory.code),
    tooltip: metaCategory.displayName,
    active: openMetaCategoryCode === metaCategory.code,
    // Named rather than decorative: a toolbar button takes no aria-label and its tooltip is hover-only, so the image carries the button's whole
    // accessible name.
    icon: <ResourceIconImage iconPath={metaCategory.iconPath} size={RESOURCE_BUTTON_ICON_SIZE} alt={metaCategory.displayName} />,
    onClick: () => onToggleResourcePanel(metaCategory.code),
  }));
}
