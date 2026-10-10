import { useMemo } from "react";
import { FaCheck, FaFish, FaMountain, FaRegFolderOpen, FaTree, FaWater } from "react-icons/fa6";
import { BiExport, BiLocationPlus } from "react-icons/bi";
import { SlClose } from "react-icons/sl";
import { VscNewFile } from "react-icons/vsc";
import { FaPaintBrush } from "react-icons/fa";
import { HiMagnifyingGlass } from "react-icons/hi2";

import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { DropButton, HamburgerMenu, Spinner } from "@/components/ui";
import type { DropButtonItem } from "@/components/ui/DropButton";
import ToolbarExclusiveButtons from "@/components/ui/toolbar/ToolbarExclusiveButtons";

import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { MapResourceCategory } from "@/types/map/MapResource";
import MapResourcePalette from "@/features/mapgallery/components/mapResources/MapResourcePalette";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/api/mapGalleryApi";
import { useAccountMenu } from "@/features/auth/hooks/useAccountMenu";

import BrushPreview from "@/features/mapgallery/components/BrushPreview";
import type { BrushKind } from "@/features/mapgallery/tools/brush";
import { iconMap } from "@/features/mapgallery/components/mapEditorIcons";
import type { ActiveDialog, ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";
import type { DrawableRiverWidth } from "@/components/map/RiverStrokeLayer";
import { IoInformationCircle } from "react-icons/io5";
import type { BiomeType } from "@/types/map/Biome";
import type { CampaignMapGeographyType } from "@/types/map/Geography";

type Args = {
  biomesLoading: boolean;
  biomes: Biome[] | undefined;
  biomeMapSelection: BiomeType | null;
  setBiomeMapSelection: (v: BiomeType | null) => void;
  openBiomes: () => void;

  geographiesLoading: boolean;
  geographies: Geography[] | undefined;
  geographyMapSelection: CampaignMapGeographyType | null;
  setGeographyMapSelection: (v: CampaignMapGeographyType | null) => void;
  openGeographies: () => void;

  mapResourcesLoading: boolean;
  mapResourceCategories: MapResourceCategory[] | undefined;
  mapResourceMapSelection: number | null;
  /** Chooses the MapResource to place (or none). Choosing one also arms the MapResource tool. */
  selectMapResource: (typeId: number | null) => void;
  openMapResources: () => void;

  openMapIndex: CampaignMapGalleryIndex | null;
  closeMap: () => void;
  openPushOutMap: () => void;
  isPushingOutMap: boolean;
  setActiveDialog: (d: ActiveDialog) => void;
  openMapProperties: () => void;

  activeTool: ActiveTool;
  setActiveTool: (t: ActiveTool) => void;

  /** The width the River tool draws. Clicking an edge that already has it erases it instead, so there is no separate eraser. */
  riverWidthSelection: DrawableRiverWidth;
  /** Chooses the river width. Choosing one also arms the River tool. */
  selectRiverWidth: (width: DrawableRiverWidth) => void;

  brushKind: BrushKind;
  setBrushKind: (k: BrushKind) => void;
  brushRadii: Record<BrushKind, number>;
  setBrushRadii: (updater: (prev: Record<BrushKind, number>) => Record<BrushKind, number>) => void;
  clampRadius: (kind: BrushKind, value: number) => number;
};

// ---------------------------------------------------------------------------
// Brush palette descriptors
// ---------------------------------------------------------------------------

/**
 * Describes one paint-brush shape. The six brushes only differ by their kind and
 * the tooltip wording; all share the same select/scroll-to-resize behavior, so we
 * drive them from this list instead of repeating ~20 lines of JSX per brush.
 *
 * `titleFor` receives the (clamped) radius and the resulting span (1 + radius * 2).
 */
type BrushDescriptor = {
  kind: BrushKind;
  titleFor: (radius: number, span: number) => string;
};

const BRUSH_KINDS: BrushDescriptor[] = [
  { kind: "square", titleFor: (_r, n) => `Square ${n}×${n} (${n * n} tiles) — scroll to resize` },
  { kind: "diamond", titleFor: (r, n) => `Diamond (radius ${r}, bounds ${n}×${n}) — scroll to resize` },
  { kind: "vline", titleFor: (_r, n) => `Vertical line (${n} tiles) — scroll to resize` },
  { kind: "hline", titleFor: (_r, n) => `Horizontal line (${n} tiles) — scroll to resize` },
  { kind: "diagDown", titleFor: (_r, n) => `Diagonal \\ (${n} tiles) — scroll to resize` },
  { kind: "diagUp", titleFor: (_r, n) => `Diagonal / (${n} tiles) — scroll to resize` },
];

type BrushItemContext = {
  brushKind: BrushKind;
  brushRadii: Record<BrushKind, number>;
  clampRadius: (kind: BrushKind, value: number) => number;
  setActiveTool: (t: ActiveTool) => void;
  setBrushKind: (k: BrushKind) => void;
  setBrushRadii: (updater: (prev: Record<BrushKind, number>) => Record<BrushKind, number>) => void;
};

/** Builds the dropdown menu item for a single brush descriptor. */
function makeBrushItem(descriptor: BrushDescriptor, ctx: BrushItemContext): DropButtonItem {
  const { kind, titleFor } = descriptor;
  const { brushKind, brushRadii, clampRadius, setActiveTool, setBrushKind, setBrushRadii } = ctx;

  // Radii are kept clamped by MapGalleryEditor's clamp-on-resize effect, so the
  // displayed radius is the clamped stored value.
  const radius = clampRadius(kind, brushRadii[kind]);
  const span = 1 + radius * 2;

  return {
    key: kind,
    label: <BrushPreview kind={kind} radius={radius} />,
    title: titleFor(radius, span),
    active: brushKind === kind,
    onSelect: () => {
      setActiveTool("paint");
      setBrushKind(kind);
    },
    onWheel: (dir) => {
      setActiveTool("paint");
      setBrushKind(kind);
      setBrushRadii((prev) => {
        const current = prev[kind] ?? 0;
        const next = clampRadius(kind, current + (dir === "up" ? 1 : -1));
        return { ...prev, [kind]: next };
      });
    },
  };
}

// ---------------------------------------------------------------------------
// River tool menu items
// ---------------------------------------------------------------------------

/**
 * What the River tool can draw, widest first.
 *
 * There is no eraser: clicking an edge that already carries the selected width removes it, so the tool toggles and the width
 * chosen here is the only thing the menu has to say.
 */
const RIVER_WIDTH_CHOICES: { width: DrawableRiverWidth; label: string; title: string; previewPx: number }[] = [
  { width: "Large", label: "Large", title: "Large river — no Land Force can cross it, and Water Forces can sail up it. Click it again to erase it", previewPx: 6 },
  { width: "Medium", label: "Medium", title: "Medium river — a Land Force can cross it, at a cost. Click it again to erase it", previewPx: 3 },
];

/**
 * Builds the River dropdown's items: the two widths, drawn at the thickness they will appear on the map.
 *
 * The preview is the line itself rather than an icon, because the only thing that distinguishes Large from Medium once it is on
 * the map is how thick it is.
 */
function buildRiverWidthItems(selection: DrawableRiverWidth, selectRiverWidth: (width: DrawableRiverWidth) => void): DropButtonItem[] {
  return RIVER_WIDTH_CHOICES.map((choice) => ({
    key: choice.width,
    label: (
      <span className="inline-flex w-full items-center gap-2">
        <span aria-hidden className="inline-flex h-4 w-6 items-center">
          <span className="w-full rounded-sm" style={{ height: choice.previewPx, background: "var(--campaign-river)" }} />
        </span>
        <span>{choice.label}</span>
      </span>
    ),
    title: choice.title,
    active: selection === choice.width,
    onSelect: () => selectRiverWidth(choice.width),
  }));
}

// ---------------------------------------------------------------------------
// Palette menu item builders (biomes / geographies)
// ---------------------------------------------------------------------------

/** Builds the Biomes dropdown items: a "<none>" entry followed by paintable biomes. */
function buildBiomePaletteItems(biomes: Biome[] | undefined, selection: BiomeType | null, setSelection: (v: BiomeType | null) => void): DropButtonItem[] {
  return [
    {
      key: "none",
      label: (
        <span className="inline-flex w-full items-center gap-2">
          <span className="inline-block h-3 w-3 border border-black/20" />
          <span className="inline-flex items-center">
            <span>{"<none>"}</span>
            {selection == null ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
          </span>
        </span>
      ),
      active: selection == null,
      onSelect: () => setSelection(null),
    },
    ...(biomes ?? [])
      .slice()
      .filter((b) => b.isPaintableOnCampaignMap)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((b) => {
        const active = selection === b.biomeType;
        const color = (b.mapEditorHtmlColor ?? "").trim();

        return {
          key: b.biomeType,
          label: (
            <span className="inline-flex w-full items-center gap-2">
              <span className="inline-block h-3 w-3 border border-black/20" style={{ background: color || "transparent" }} />
              <span className="inline-flex items-center">
                <span>{b.name}</span>
                {active ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
              </span>
            </span>
          ),
          active,
          onSelect: () => setSelection(b.biomeType),
        };
      }),
  ];
}

/** Builds the Geographies dropdown items: a "<none>" entry followed by paintable geographies. */
function buildGeographyPaletteItems(
  geographies: Geography[] | undefined,
  selection: CampaignMapGeographyType | null,
  setSelection: (v: CampaignMapGeographyType | null) => void,
): DropButtonItem[] {
  return [
    {
      key: "none",
      label: (
        <span className="inline-flex w-full items-center gap-2">
          <span className="inline-flex h-3 w-3 items-center justify-center" />
          <span className="inline-flex items-center">
            <span>{"<none>"}</span>
            {selection == null ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
          </span>
        </span>
      ),
      active: selection == null,
      onSelect: () => setSelection(null),
    },
    ...(geographies ?? [])
      .slice()
      .filter((g) => g.isPaintableOnCampaignMap)
      .sort((a, b) => a.relativeElevation - b.relativeElevation)
      .map((g) => {
        const active = selection === g.campaignMapGeographyType;
        const iconKey = (g.mapEditorIcon ?? "").trim();
        const Icon = iconKey ? iconMap[iconKey] : undefined;

        return {
          key: g.campaignMapGeographyType,
          label: (
            <span className="inline-flex w-full items-center gap-2">
              <span className="inline-flex h-3 w-3 items-center justify-center">
                {Icon ? <Icon aria-hidden size={12} className="text-slate-700" /> : null}
              </span>
              <span className="inline-flex items-center">
                <span>{g.name}</span>
                {active ? <FaCheck aria-hidden size={12} className="ml-1 text-slate-600" /> : null}
              </span>
            </span>
          ),
          active,
          onSelect: () => setSelection(g.campaignMapGeographyType),
        };
      }),
  ];
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export function useMapGalleryEditorToolbar(args: Args): ToolBarGroup[] {
  const {
    biomesLoading,
    biomes,
    biomeMapSelection,
    setBiomeMapSelection,
    openBiomes,

    geographiesLoading,
    geographies,
    geographyMapSelection,
    setGeographyMapSelection,
    openGeographies,

    mapResourcesLoading,
    mapResourceCategories,
    mapResourceMapSelection,
    selectMapResource,
    openMapResources,

    openMapIndex,
    closeMap,
    openPushOutMap,
    isPushingOutMap,
    setActiveDialog,
    openMapProperties,

    activeTool,
    setActiveTool,

    riverWidthSelection,
    selectRiverWidth,

    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    clampRadius,
  } = args;

  // The signed-in username and the Log out item, shared with every other page's hamburger menu.
  const { headerLabel: accountHeader, group: accountGroup } = useAccountMenu();

  // Each toolbar group is memoized independently with narrow deps so an unrelated
  // change (e.g. a brush resize) doesn't rebuild the whole toolbar object graph.
  const mapMenuGroup = useMemo<ToolBarGroup>(
    () => ({
      caption: "Map",
      buttons: [
        {
          key: "map-menu",
          render: () => (
            <HamburgerMenu
              title="Map menu"
              tooltip="Map"
              header={accountHeader}
              groups={[
                {
                  label: "Map",
                  items: [
                    {
                      key: "new",
                      icon: <VscNewFile />,
                      label: "New",
                      tooltip: "New Campaign Map",
                      onClick: () => setActiveDialog("newCampaignMap"),
                    },
                    {
                      key: "open",
                      icon: <FaRegFolderOpen />,
                      label: "Open",
                      tooltip: "Open Campaign Map",
                      onClick: () => setActiveDialog("openCampaignMap"),
                    },
                    {
                      key: "close",
                      icon: <SlClose />,
                      label: "Close",
                      tooltip: "Close Campaign Map",
                      disabled: openMapIndex == null,
                      onClick: closeMap,
                    },
                  ],
                },
                {
                  label: "Dev Tools",
                  items: [
                    {
                      key: "push-out",
                      icon: isPushingOutMap ? <Spinner size="sm" /> : <BiExport />,
                      label: isPushingOutMap ? "Pushing Out..." : "Push Out",
                      tooltip: "Push out map changes to currently played games",
                      disabled: openMapIndex == null || isPushingOutMap,
                      onClick: openPushOutMap,
                    },
                  ],
                },
                // Log out last, separated from the page's own groups. Absent when logged out.
                ...(accountGroup ? [accountGroup] : []),
              ]}
            />
          ),
        },
      ],
    }),
    [openMapIndex, closeMap, openPushOutMap, isPushingOutMap, setActiveDialog, accountHeader, accountGroup],
  );

  const paletteGroup = useMemo<ToolBarGroup>(
    () => ({
      caption: "Palette",
      buttons: [
        {
          key: "biomes",
          tooltip: biomesLoading ? "Loading Biomes..." : "Biomes",
          disabled: biomesLoading,
          render: ({ disabled }) => (
            <DropButton
              label="Biomes"
              title={biomesLoading ? "Loading Biomes..." : "Biomes"}
              icon={<FaTree />}
              disabled={disabled}
              onClick={openBiomes}
              menuDisabled={biomesLoading || openMapIndex == null}
              items={buildBiomePaletteItems(biomes, biomeMapSelection, setBiomeMapSelection)}
            />
          ),
        },
        {
          key: "geographies",
          tooltip: geographiesLoading ? "Loading Geographies..." : "Geographies",
          disabled: geographiesLoading,
          render: ({ disabled }) => (
            <DropButton
              label="Geographies"
              title={geographiesLoading ? "Loading Geographies..." : "Geographies"}
              icon={<FaMountain />}
              disabled={disabled}
              onClick={openGeographies}
              menuDisabled={geographiesLoading || openMapIndex == null}
              items={buildGeographyPaletteItems(geographies, geographyMapSelection, setGeographyMapSelection)}
            />
          ),
        },
        {
          // The main click opens the MapResource Type Manager, as the Biomes and Geographies buttons open theirs; the dropdown lists every
          // MapResource grouped by category, and choosing one arms the MapResource tool to place it.
          key: "mapResources",
          tooltip: mapResourcesLoading ? "Loading MapResources..." : "MapResources",
          disabled: mapResourcesLoading,
          render: ({ disabled }) => (
            <DropButton
              label="MapResources"
              title={mapResourcesLoading ? "Loading MapResources..." : "MapResources"}
              icon={<FaFish />}
              disabled={disabled}
              onClick={openMapResources}
              menuDisabled={mapResourcesLoading || openMapIndex == null}
              items={[]}
              renderMenu={(close) => (
                <MapResourcePalette categories={mapResourceCategories ?? []} selectedTypeId={mapResourceMapSelection} onSelect={selectMapResource} close={close} />
              )}
            />
          ),
        },
      ],
    }),
    [
      biomesLoading,
      biomes,
      biomeMapSelection,
      setBiomeMapSelection,
      openBiomes,
      geographiesLoading,
      geographies,
      geographyMapSelection,
      setGeographyMapSelection,
      openGeographies,
      mapResourcesLoading,
      mapResourceCategories,
      mapResourceMapSelection,
      selectMapResource,
      openMapResources,
      openMapIndex,
    ],
  );

  const editorToolsGroup = useMemo<ToolBarGroup>(
    () => ({
      caption: "Editor Tools",
      buttons: [
        {
          key: "tools",
          disabled: openMapIndex == null,
          render: ({ disabled }) => (
            <ToolbarExclusiveButtons
              disabled={disabled}
              value={activeTool}
              onChange={(k) => setActiveTool(k as ActiveTool)}
              items={[
                { type: "button", key: "inspect", icon: <HiMagnifyingGlass />, tooltip: "Inspection" },
                { type: "button", key: "startPosition", icon: <BiLocationPlus />, tooltip: "Start Positions" },
                {
                  type: "dropbutton",
                  key: "paint",
                  label: "Paint",
                  icon: <FaPaintBrush />,
                  tooltip: "Paint Biome & Geography",
                  onClick: () => {
                    // main click just selects the tool; brush is chosen from the dropdown.
                  },
                  items: BRUSH_KINDS.map((descriptor) =>
                    makeBrushItem(descriptor, { brushKind, brushRadii, clampRadius, setActiveTool, setBrushKind, setBrushRadii }),
                  ),
                  menuDisabled: false,
                  menuLayout: "grid",
                  menuGridColumns: 3,
                },
                {
                  type: "dropbutton",
                  key: "river",
                  label: "River",
                  icon: <FaWater />,
                  tooltip: "Draw rivers along tile edges. Clicking an edge that already carries the chosen width erases it",
                  onClick: () => {
                    // The main click only arms the tool; the width comes from the dropdown, and whatever was chosen last stays chosen.
                  },
                  items: buildRiverWidthItems(riverWidthSelection, selectRiverWidth),
                  menuDisabled: false,
                },
              ]}
            />
          ),
        },
        {
          key: "properties",
          icon: <IoInformationCircle />,
          tooltip: "Map Properties",
          disabled: openMapIndex == null,
          onClick: openMapProperties,
        },
      ],
    }),
    [
      openMapIndex,
      activeTool,
      setActiveTool,
      brushKind,
      brushRadii,
      clampRadius,
      setBrushKind,
      setBrushRadii,
      openMapProperties,
      riverWidthSelection,
      selectRiverWidth,
    ],
  );

  return useMemo(() => [mapMenuGroup, paletteGroup, editorToolsGroup], [mapMenuGroup, paletteGroup, editorToolsGroup]);
}
