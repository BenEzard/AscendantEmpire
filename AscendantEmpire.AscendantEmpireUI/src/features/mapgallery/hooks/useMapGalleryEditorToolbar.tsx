import { useMemo } from "react";
import { FaCheck, FaMountain, FaRegFolderOpen, FaTree } from "react-icons/fa6";
import { BiExport, BiLocationPlus } from "react-icons/bi";
import { SlClose } from "react-icons/sl";
import { VscNewFile } from "react-icons/vsc";
import { FaPaintBrush } from "react-icons/fa";
import { HiMagnifyingGlass } from "react-icons/hi2";

import type { ToolBarGroup } from "@/components/Toolbar";
import { DropButton, HamburgerMenu, Spinner } from "@/components/ui";
import type { DropButtonItem } from "@/components/ui/DropButton";
import ToolbarExclusiveButtons from "@/components/ToolbarExclusiveButtons";

import type { Biome } from "@/types/map/Biome";
import type { Geography } from "@/types/map/Geography";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/components/OpenCampaignMapDialog";

import BrushPreview from "@/features/mapgallery/components/BrushPreview";
import type { BrushKind } from "@/features/mapgallery/tools/brush";
import { iconMap } from "@/features/mapgallery/components/mapEditorIcons";
import type { ActiveDialog, ActiveTool } from "@/features/mapgallery/types/MapGalleryEditorTypes";
import { IoInformationCircle } from "react-icons/io5";

type Args = {
  biomesLoading: boolean;
  biomes: Biome[] | undefined;
  biomeMapSelection: number | null;
  setBiomeMapSelection: (v: number | null) => void;
  openBiomes: () => void;

  geographiesLoading: boolean;
  geographies: Geography[] | undefined;
  geographyMapSelection: number | null;
  setGeographyMapSelection: (v: number | null) => void;
  openGeographies: () => void;

  openMapIndex: CampaignMapGalleryIndex | null;
  closeMap: () => void;
  openPushOutMap: () => void;
  isPushingOutMap: boolean;
  setActiveDialog: (d: ActiveDialog) => void;
  openMapProperties: () => void;

  activeTool: ActiveTool;
  setActiveTool: (t: ActiveTool) => void;

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
// Palette menu item builders (biomes / geographies)
// ---------------------------------------------------------------------------

/** Builds the Biomes dropdown items: a "<none>" entry followed by paintable biomes. */
function buildBiomePaletteItems(biomes: Biome[] | undefined, selection: number | null, setSelection: (v: number | null) => void): DropButtonItem[] {
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
  selection: number | null,
  setSelection: (v: number | null) => void,
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

    openMapIndex,
    closeMap,
    openPushOutMap,
    isPushingOutMap,
    setActiveDialog,
    openMapProperties,

    activeTool,
    setActiveTool,

    brushKind,
    setBrushKind,
    brushRadii,
    setBrushRadii,
    clampRadius,
  } = args;

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
              ]}
            />
          ),
        },
      ],
    }),
    [openMapIndex, closeMap, openPushOutMap, isPushingOutMap, setActiveDialog],
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
    [openMapIndex, activeTool, setActiveTool, brushKind, brushRadii, clampRadius, setBrushKind, setBrushRadii, openMapProperties],
  );

  return useMemo(() => [mapMenuGroup, paletteGroup, editorToolsGroup], [mapMenuGroup, paletteGroup, editorToolsGroup]);
}
