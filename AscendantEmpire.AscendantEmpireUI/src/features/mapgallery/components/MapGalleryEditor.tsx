import { FaMountain, FaRegFolderOpen, FaTree } from "react-icons/fa6";
import ToolBar, { type ToolBarGroup } from "@/components/Toolbar";
import { useCallback, useMemo, useState } from "react";
import type { Biome } from "../../../types/map/Biome";
import { Alert, Spinner, Toast, ToastContainer } from "react-bootstrap";
import type { Geography } from "../../../types/map/Geography";
import { VscNewFile } from "react-icons/vsc";
import NewCampaignMapDialog from "./NewCampaignMapDialog";
import OpenCampaignMapDialog, { type CampaignMapGalleryIndex } from "./OpenCampaignMapDialog";
import { useCampaignMapBiomesQuery } from "../hooks/useCampaignMapBiomes";
import BiomeManagerDialog from "./BiomeManagerDialog";
import GeographyManagerDialog from "./GeographyManagerDialog";
import { useCampaignMapTilesQuery } from "@/features/mapgallery/hooks/useCampaignMapTiles";
import { useCampaignMapGeographiesQuery } from "../hooks/useCampaignMapGeographies";
import type { TileCell } from "./TileTypes";
import TileGrid from "./TileGrid";

function buildTiles2d(
  width: number,
  height: number,
  tiles: { xCoord: number; yCoord: number; biomeTypeId: number; campaignMapGeographyTypeId: number }[]
): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
    }))
  );

  for (const t of tiles) {
    if (t.xCoord < 0 || t.xCoord >= width) continue;
    if (t.yCoord < 0 || t.yCoord >= height) continue;

    grid[t.yCoord][t.xCoord] = {
      x: t.xCoord,
      y: t.yCoord,
      biomeTypeId: t.biomeTypeId,
      geographyTypeId: t.campaignMapGeographyTypeId,
    };
  }

  return grid;
}

function MapGalleryEditor() {
  type ActiveDialog = "biomes" | "geographies" | "newCampaignMap" | "openCampaignMap" | null;

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);

  // --- Biomes (draft owned by parent) ---
  const [biomeDraft, setBiomeDraft] = useState<Biome[]>([]);
  const [selectedBiomeType, setSelectedBiomeType] = useState<number | null>(null);

  // --- Geographies (draft owned by parent) ---
  const [geographyDraft, setGeographyDraft] = useState<Geography[]>([]);
  const [selectedGeographyType, setSelectedGeographyType] = useState<number | null>(null);

  // "Open map" selection
  const [openMapIndex, setOpenMapIndex] = useState<CampaignMapGalleryIndex | null>(null);

  // ---------------------------
  // Toast
  // ---------------------------
  const [toast, setToast] = useState<{
    show: boolean;
    message: string;
    variant: "success" | "danger" | "warning" | "info";
  }>({
    show: false,
    message: "",
    variant: "success",
  });

  const openToast = useCallback((message: string, variant: "success" | "danger" | "warning" | "info" = "success") => {
    setToast({ show: true, message, variant });
  }, []);

  const onTileClick = useCallback(
    (t: TileCell) => {
      openToast(`Tile (${t.x},${t.y}) biome=${t.biomeTypeId ?? "null"} geo=${t.geographyTypeId ?? "null"}`, "info");
    },
    [openToast]
  );

  const closeToast = () => setToast((t) => ({ ...t, show: false }));

  // ---------------------------
  // Queries
  // ---------------------------
  const { data: biomes, error: biomeError, isLoading: biomesLoading } = useCampaignMapBiomesQuery();

  const { data: geographies, error: geographyError, isLoading: geographiesLoading } = useCampaignMapGeographiesQuery();

  {
    biomeError && <Alert variant="danger">Biomes failed: {biomeError.message}</Alert>;
  }
  {
    geographyError && <Alert variant="danger">Geographies failed: {geographyError.message}</Alert>;
  }

  // ---------------------------
  // Lookups for tile view
  // ---------------------------
  const biomeColorByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) {
      const c = (b.mapEditorHtmlColor ?? "").trim();
      if (c) m.set(b.biomeType, c);
    }
    return m;
  }, [biomes]);

  const biomeNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const b of biomes ?? []) m.set(b.biomeType, b.name);
    return m;
  }, [biomes]);

  const geographyNameByType = useMemo(() => {
    const m = new Map<number, string>();
    for (const g of geographies ?? []) {
      // assuming your Geography id is campaignMapGeographyType (as in your dialog)
      m.set(g.campaignMapGeographyType, g.name);
    }
    return m;
  }, [geographies]);

  // ---------------------------
  // Open map / tiles
  // ---------------------------
  const {
    data: tilesResponse,
    error: tilesError,
    isLoading: tilesLoading,
    isFetching: tilesFetching,
  } = useCampaignMapTilesQuery(openMapIndex?.campaignMapGalleryIndexId ?? null, openMapIndex != null);

  const tiles2d = useMemo(() => {
    if (!openMapIndex || !tilesResponse) return null;
    return buildTiles2d(openMapIndex.width, openMapIndex.height, tilesResponse.tiles);
  }, [openMapIndex, tilesResponse]);

  // ---------------------------
  // Biomes: draft helpers
  // ---------------------------
  const getFirstBiomeType = (list: Biome[]) => list.slice().sort((a, b) => a.name.localeCompare(b.name))[0]?.biomeType ?? null;

  const BIOME_DEFAULTS = {
    regenerationRate: 1.1,
    movementCost: 1,
    isTraversable: true,
    isWater: false,
    isPaintableOnCampaignMap: true,
    doesBlockSight: false,
  };

  const createNewBiome = (nextId: number): Biome => ({
    biomeType: nextId,
    name: "new",
    isTraversable: BIOME_DEFAULTS.isTraversable,
    movementCost: BIOME_DEFAULTS.movementCost,
    isWater: BIOME_DEFAULTS.isWater,
    doesBlockSight: BIOME_DEFAULTS.doesBlockSight,
    canBeElevated: false,
    isPaintableOnCampaignMap: BIOME_DEFAULTS.isPaintableOnCampaignMap,
    mapEditorHtmlColor: "",
    regenerationRate: BIOME_DEFAULTS.regenerationRate,
    baseForageValue: null,
    baseHuntValue: null,
    baseFishValue: null,
    baseFodderValue: null,
    baseBoneValue: null,
    baseWoodValue: null,
    baseStoneValue: null,
    baseSpecialValue: null,
  });

  const addNewBiomeAndOpen = () => {
    const baseList = activeDialog === "biomes" ? biomeDraft : biomes ?? [];
    const nextId = Math.max(0, ...baseList.map((b) => b.biomeType)) + 1;

    const newBiome = createNewBiome(nextId);

    setBiomeDraft([...baseList, newBiome]);
    setSelectedBiomeType(newBiome.biomeType);
    setActiveDialog("biomes");
  };

  // ---------------------------
  // Geographies: draft helpers (mirrors Biomes)
  // ---------------------------
  const getFirstGeographyType = (list: Geography[]) => list.slice().sort((a, b) => a.name.localeCompare(b.name))[0]?.campaignMapGeographyType ?? null;

  const GEO_DEFAULTS = {
    relativeElevation: 0,
    movementCost: 1,
    isPaintableOnCampaignMap: true,
  };

  const createNewGeography = (nextId: number): Geography => ({
    campaignMapGeographyType: nextId,
    name: "new",
    relativeElevation: GEO_DEFAULTS.relativeElevation,
    movementCost: GEO_DEFAULTS.movementCost,
    mapEditorIcon: null,
    isPaintableOnCampaignMap: GEO_DEFAULTS.isPaintableOnCampaignMap,
  });

  const addNewGeographyAndOpen = () => {
    const baseList = activeDialog === "geographies" ? geographyDraft : geographies ?? [];
    const nextId = Math.max(0, ...baseList.map((g) => g.campaignMapGeographyType)) + 1;

    const newGeo = createNewGeography(nextId);

    setGeographyDraft([...baseList, newGeo]);
    setSelectedGeographyType(newGeo.campaignMapGeographyType);
    setActiveDialog("geographies");
  };

  const getTileStyle = useCallback(
    (t: TileCell): React.CSSProperties => {
      const bg = t.biomeTypeId != null ? biomeColorByType.get(t.biomeTypeId) : undefined;

      return {
        background: bg ?? "rgba(0,0,0,0.03)",
      };
    },
    [biomeColorByType]
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)" : "(no biome)";

      const geographyName = t.geographyTypeId != null ? geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)" : "(no geography)";

      return `(${t.x}, ${t.y}); ${biomeName}, ${geographyName}`;
    },
    [biomeNameByType, geographyNameByType]
  );

  // ---------------------------
  // ToolBar
  // ---------------------------
  const openBiomes = useCallback(() => {
    if (biomesLoading) return;
    const list = biomes ?? [];
    setBiomeDraft(list);
    setSelectedBiomeType(getFirstBiomeType(list));
    setActiveDialog("biomes");
  }, [biomesLoading, biomes]);

  const openGeographies = useCallback(() => {
    if (geographiesLoading) return;
    const list = geographies ?? [];
    setGeographyDraft(list);
    setSelectedGeographyType(getFirstGeographyType(list));
    setActiveDialog("geographies");
  }, [geographiesLoading, geographies]);

  const middleAddBiome = useCallback(() => {
    if (biomesLoading) return;
    addNewBiomeAndOpen();
  }, [biomesLoading, addNewBiomeAndOpen]);

  const middleAddGeography = useCallback(() => {
    if (geographiesLoading) return;
    addNewGeographyAndOpen();
  }, [geographiesLoading, addNewGeographyAndOpen]);

  const toolBarGroups = useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "File",
        buttons: [
          { key: "new", icon: <VscNewFile />, tooltip: "New Campaign Map", onClick: () => setActiveDialog("newCampaignMap") },
          { key: "open", icon: <FaRegFolderOpen />, tooltip: "Open Campaign Map", onClick: () => setActiveDialog("openCampaignMap") },
        ],
      },
      {
        caption: "Palette",
        buttons: [
          {
            key: "biomes",
            icon: <FaTree />,
            tooltip: biomesLoading ? "Loading Biomes..." : "Biomes",
            onClick: openBiomes,
            onMiddleClick: middleAddBiome,
          },
          {
            key: "geographies",
            icon: <FaMountain />,
            tooltip: geographiesLoading ? "Loading Geographies..." : "Geographies",
            onClick: openGeographies,
            onMiddleClick: middleAddGeography,
          },
        ],
      },
    ],
    [biomesLoading, geographiesLoading, openBiomes, middleAddBiome, openGeographies, middleAddGeography]
  );

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      {/* ====== Opened map display ====== */}
      <div className="mt-3">
        {openMapIndex == null ? (
          <Alert variant="secondary" className="mb-0">
            No map opened. Use <strong>File → Open</strong>.
          </Alert>
        ) : (
          <>
            <div className="d-flex align-items-center justify-content-between mb-2">
              <div>
                <strong>{openMapIndex.mapName}</strong>{" "}
                <span className="text-muted">
                  ({openMapIndex.width}×{openMapIndex.height})
                </span>
                <div className="text-muted" style={{ fontSize: 12 }}>
                  {openMapIndex.description}
                </div>
              </div>

              {(tilesLoading || tilesFetching) && (
                <div className="d-flex align-items-center gap-2 text-muted">
                  <Spinner animation="border" size="sm" />
                  <span>Loading tiles...</span>
                </div>
              )}
            </div>

            {tilesError && <Alert variant="danger">Failed to load tiles: {tilesError.message}</Alert>}

            {tiles2d && (
              <TileGrid
                width={openMapIndex.width}
                height={openMapIndex.height}
                tiles2d={tiles2d}
                geographies={geographies}
                onTileClick={onTileClick}
                getTileStyle={getTileStyle}
                getTileTitle={getTileTitle}
              />
            )}
          </>
        )}
      </div>

      <BiomeManagerDialog
        show={activeDialog === "biomes"}
        biomes={biomeDraft}
        selectedBiomeType={selectedBiomeType}
        onSelectBiomeType={setSelectedBiomeType}
        setBiomes={setBiomeDraft}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <GeographyManagerDialog
        show={activeDialog === "geographies"}
        geographies={geographyDraft}
        selectedGeographyType={selectedGeographyType}
        onSelectGeographyType={setSelectedGeographyType}
        setGeographies={setGeographyDraft}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <NewCampaignMapDialog
        show={activeDialog === "newCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
      />

      <OpenCampaignMapDialog
        show={activeDialog === "openCampaignMap"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Saved!", "success")}
        onOpenMap={(index) => {
          setOpenMapIndex(index);
          setActiveDialog(null);
        }}
      />

      <ToastContainer position="bottom-end" className="p-3">
        <Toast show={toast.show} onClose={closeToast} autohide delay={3000} className={`border-0 bg-${toast.variant}-subtle`}>
          <Toast.Body className={`border-0 ${toast.variant === "warning" ? "" : `text-${toast.variant}`}`}>{toast.message}</Toast.Body>
        </Toast>
      </ToastContainer>
    </>
  );
}

export default MapGalleryEditor;
