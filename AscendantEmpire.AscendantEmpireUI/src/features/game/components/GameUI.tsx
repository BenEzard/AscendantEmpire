import type { ToolBarGroup } from "@/components/Toolbar";
import ToolBar from "@/components/Toolbar";
import { useCallback, useMemo, useState } from "react";
import { Alert, Spinner, Toast, ToastContainer } from "react-bootstrap";
import { FaRegFolderOpen } from "react-icons/fa6";
import OpenGameDialog from "./OpenGameDialog";
import { usePlayerCampaignMapTilesQuery } from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid from "@/features/map/components/TileGrid";
import type { TileCell } from "@/features/map/components/TileTypes";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import type { OpenGameSelection } from "../types/OpenGameSelection";

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

function GameUI() {
  type ActiveDialog = "openGame" | null;

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);

  // "Open map" selection
  const [gameSelection, setGameSelection] = useState<OpenGameSelection | null>(null);

  // ---------------------------
  // ToolBar
  // ---------------------------
  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "Game",
      buttons: [{ key: "open", icon: <FaRegFolderOpen />, tooltip: "Open Game", onClick: () => setActiveDialog("openGame") }],
    },
  ];

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
  const gameGuid = gameSelection?.gameGuid;
  const playerGuid = gameSelection?.playerGuid;

  const tilesEnabled = !!gameGuid && !!playerGuid;

  console.log("gameSelection:", gameSelection);
  console.log("gameGuid/playerGuid:", gameSelection?.gameGuid, gameSelection?.playerGuid);

  const {
    data: tilesResponse,
    error: tilesError,
    isLoading: tilesLoading,
    isFetching: tilesFetching,
  } = usePlayerCampaignMapTilesQuery(gameGuid, playerGuid, tilesEnabled);

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;
    return buildTiles2d(gameSelection.width, gameSelection.height, tilesResponse.tiles);
  }, [gameSelection, tilesResponse]);

  // On tile click we want to show a toast with tile info: [x, y]: Biome, Geography
  const onTileClick = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId == null ? "null" : biomeNameByType.get(t.biomeTypeId) ?? `unknown(${t.biomeTypeId})`;

      const geoName = t.geographyTypeId == null ? "null" : geographyNameByType.get(t.geographyTypeId) ?? `unknown(${t.geographyTypeId})`;

      // If biomeName is "Unknown" and GeographyName is "Unknown" then just show "Unknown" once.
      const label = biomeName === "Unknown" && geoName === "Unknown" ? "Unknown" : `${biomeName}, ${geoName}`;

      openToast(`[${t.x}, ${t.y}]: ${label}`, "warning");
    },
    [openToast, biomeNameByType, geographyNameByType]
  );

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

      // If biomeName is "Unknown" and GeographyName is "Unknown" then just show "Unknown" once.
      const label = biomeName === "Unknown" && geographyName === "Unknown" ? "Unknown" : `${biomeName}, ${geographyName}`;

      return `(${t.x}, ${t.y}); ${label}`;
    },
    [biomeNameByType, geographyNameByType]
  );

  return (
    <>
      <ToolBar groups={toolBarGroups} />

      <OpenGameDialog
        show={activeDialog === "openGame"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Undefined", "success")}
        onOpenMap={(selection) => {
          setGameSelection(selection);
          setActiveDialog(null);
        }}
      />

      <ToastContainer position="bottom-end" className="p-3">
        <Toast show={toast.show} onClose={closeToast} autohide delay={3000} className={`border-0 bg-${toast.variant}-subtle`}>
          <Toast.Body className={`border-0 ${toast.variant === "warning" ? "" : `text-${toast.variant}`}`}>{toast.message}</Toast.Body>
        </Toast>
      </ToastContainer>

      {/* ====== Opened map display ====== */}
      <div className="mt-3">
        {gameSelection == null ? (
          <Alert variant="secondary" className="mb-0">
            No game opened. Use <strong>Game → Open</strong>.
          </Alert>
        ) : (
          <>
            <div className="d-flex align-items-center justify-content-between mb-2">
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
                width={gameSelection.width}
                height={gameSelection.height}
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
    </>
  );
}

export default GameUI;
