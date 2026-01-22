import type { ToolBarGroup } from "@/components/Toolbar";
import ToolBar from "@/components/Toolbar";
import { useCallback, useMemo, useState, useEffect, useRef } from "react";
import { Alert, Spinner, Toast, ToastContainer } from "react-bootstrap";
import { FaRegFolderOpen } from "react-icons/fa6";
import OpenGameDialog from "./OpenGameDialog";
import {
  useClearPlayerCampaignMapMutation,
  useLoadPlayerCampaignMapMutation,
  usePlayerCampaignMapTilesQuery,
} from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid from "@/features/map/components/TileGrid";
import type { TileCell } from "@/features/map/components/TileTypes";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import type { OpenGameSelection } from "../types/OpenGameSelection";
import { GoEye, GoEyeClosed } from "react-icons/go";
import type { LoadPlayerCampaignMapRequest, PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";

function buildTiles2d(
  width: number,
  height: number,
  tiles: { xCoord: number; yCoord: number; biomeTypeId: number; campaignMapGeographyTypeId: number }[],
): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
    })),
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
  type ActiveTool = "vision" | null;

  type SelectedTile = { x: number; y: number };

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [activeTool, setActiveTool] = useState<ActiveTool>(null);
  const [selectedTile, setSelectedTile] = useState<SelectedTile | null>(null);
  const [visionObservers, setVisionObservers] = useState<PlayerCampaignMapObserverResultDto[]>([]);

  const visionLastRequestKeyRef = useRef<string | null>(null);
  const visionInFlightRef = useRef(false);
  const visionMutateRef = useRef<null | ((payload: LoadPlayerCampaignMapRequest) => void)>(null);
  const prevActiveToolRef = useRef<ActiveTool>(null);

  // "Open map" selection
  const [gameSelection, setGameSelection] = useState<OpenGameSelection | null>(null);

  const gameGuid = gameSelection?.gameGuid;
  const playerGuid = gameSelection?.playerGuid;
  const tilesEnabled = !!gameGuid && !!playerGuid;

  // If the context needed for a tool goes away, clear the active tool.
  useEffect(() => {
    if (gameSelection == null) {
      setActiveTool(null);
      setSelectedTile(null);
      setVisionObservers([]);
    }
  }, [gameSelection]);

  const observedTileKeys = useMemo(() => {
    const next = new Set<string>();
    for (const o of visionObservers) {
      for (const t of o.seenTiles ?? []) next.add(`${t.xCoord},${t.yCoord}`);
    }
    return next;
  }, [visionObservers]);

  const toggleVisionTool = useCallback(() => {
    setActiveTool((t) => (t === "vision" ? null : "vision"));
  }, []);

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

  const clearPlayerCampaignMap = useClearPlayerCampaignMapMutation({
    onSuccess: () => {
      openToast("Vision cleared", "info");
    },
    onError: (error) => {
      const msg = error instanceof Error ? error.message : "Unknown error";
      openToast(`Clear vision failed: ${msg}`, "danger");
    },
  });

  const clearVision = useCallback(() => {
    // Must always update active tool immediately.
    setActiveTool(null);
    setVisionObservers([]);

    if (!gameGuid || !playerGuid) return;

    clearPlayerCampaignMap.mutate({ gameGuid, playerGuid });
  }, [clearPlayerCampaignMap, gameGuid, playerGuid]);
  // ---------------------------
  // ToolBar
  // ---------------------------
  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "Game",
      buttons: [{ key: "open", icon: <FaRegFolderOpen />, tooltip: "Open Game", onClick: () => setActiveDialog("openGame") }],
    },
    {
      caption: "Dev Tools",
      buttons: [
        {
          key: "vision",
          icon: <GoEye />,
          tooltip: "Check Vision",
          disabled: gameSelection == null,
          active: activeTool === "vision",
          onClick: toggleVisionTool,
        },
        {
          key: "clear-vision",
          icon: <GoEyeClosed />,
          tooltip: "Clear Vision",
          disabled: gameSelection == null,
          onClick: clearVision,
        },
      ],
    },
  ];

  // ---------------------------
  // Queries
  // ---------------------------
  const { data: biomes, error: biomeError } = useCampaignMapBiomesQuery();

  const { data: geographies, error: geographyError } = useCampaignMapGeographiesQuery();

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
  } = usePlayerCampaignMapTilesQuery(gameGuid, playerGuid, tilesEnabled);

  const loadPlayerCampaignMap = useLoadPlayerCampaignMapMutation({
    onSuccess: (data) => {
      visionInFlightRef.current = false;

      // Keep observer results separate from tile state/cache.
      setVisionObservers(data.observers ?? []);

      console.log("[Vision] Response:", data);
      console.log("[Vision] Counts:", {
        observerCount: data.observers?.length ?? 0,
        tileCount: data.tiles?.length ?? 0,
      });
    },
    onError: (error) => {
      visionInFlightRef.current = false;
      const msg = error instanceof Error ? error.message : "Unknown error";
      openToast(`Vision load failed: ${msg}`, "danger");
    },
  });

  // Keep a stable reference to mutate() so effects/callbacks don't re-fire due to object identity churn.
  useEffect(() => {
    visionMutateRef.current = loadPlayerCampaignMap.mutate;
  }, [loadPlayerCampaignMap.mutate]);

  const sendVisionRequest = useCallback(
    (x: number, y: number, source: "tile-click" | "vision-enabled") => {
      if (activeTool !== "vision") {
        console.log("[Vision] Skipped: activeTool is not vision", { source, activeTool, x, y });
        return;
      }

      if (!gameGuid || !playerGuid) {
        console.log("[Vision] Skipped: missing gameGuid/playerGuid", { source, gameGuid, playerGuid, x, y });
        return;
      }

      const requestKey = `${gameGuid}:${playerGuid}:${x},${y}`;

      // Dedup repeated triggers while a request is in-flight (or identical immediate repeats).
      if (visionInFlightRef.current && visionLastRequestKeyRef.current === requestKey) {
        console.log("[Vision] Deduped: request already in-flight", { source, requestKey });
        return;
      }

      if (visionLastRequestKeyRef.current === requestKey) {
        console.log("[Vision] Deduped: identical request", { source, requestKey });
        return;
      }

      const payload = {
        gameGuid,
        playerGuid,
        loadWholeMap: false,
        observers: [
          {
            temporaryObserverId: 1,
            location: { x, y },
            sightRange: 2,
          },
        ],
      };

      visionLastRequestKeyRef.current = requestKey;
      visionInFlightRef.current = true;

      console.log("[Vision] Request payload:", { source, requestKey, payload });

      const mutate = visionMutateRef.current;
      if (!mutate) {
        console.log("[Vision] Skipped: mutate ref not ready", { source, requestKey });
        visionInFlightRef.current = false;
        return;
      }

      mutate(payload);
    },
    [activeTool, gameGuid, playerGuid],
  );

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;
    return buildTiles2d(gameSelection.width, gameSelection.height, tilesResponse.tiles);
  }, [gameSelection, tilesResponse]);

  // On tile click we want to show a toast with tile info: [x, y]: Biome, Geography
  const onTileClick = useCallback(
    (t: TileCell) => {
      console.log("[TileClick]", {
        activeTool,
        gameGuid,
        playerGuid,
        tile: { x: t.x, y: t.y, biomeTypeId: t.biomeTypeId, geographyTypeId: t.geographyTypeId },
      });

      setSelectedTile({ x: t.x, y: t.y });

      // If Vision is the active tool, selecting a tile triggers a targeted map load.
      sendVisionRequest(t.x, t.y, "tile-click");

      const biomeName = t.biomeTypeId == null ? "null" : (biomeNameByType.get(t.biomeTypeId) ?? `unknown(${t.biomeTypeId})`);

      const geoName = t.geographyTypeId == null ? "null" : (geographyNameByType.get(t.geographyTypeId) ?? `unknown(${t.geographyTypeId})`);

      // If biomeName is "Unknown" and GeographyName is "Unknown" then just show "Unknown" once.
      const label = biomeName === "Unknown" && geoName === "Unknown" ? "Unknown" : `${biomeName}, ${geoName}`;

      openToast(`[${t.x}, ${t.y}]: ${label}`, "warning");
    },
    [openToast, biomeNameByType, geographyNameByType, activeTool, gameGuid, playerGuid, sendVisionRequest],
  );

  // If a tile is already selected and the user turns Vision on, run it once.
  useEffect(() => {
    const prev = prevActiveToolRef.current;
    prevActiveToolRef.current = activeTool;

    if (activeTool !== "vision") return;
    if (prev === "vision") return; // only on transition to vision
    if (!selectedTile) return;

    sendVisionRequest(selectedTile.x, selectedTile.y, "vision-enabled");
  }, [activeTool, selectedTile, sendVisionRequest]);

  const getTileStyle = useCallback(
    (t: TileCell): React.CSSProperties => {
      const bg = t.biomeTypeId != null ? biomeColorByType.get(t.biomeTypeId) : undefined;

      const baseBg = bg ?? "rgba(0,0,0,0.03)";
      const isSelected = selectedTile != null && selectedTile.x === t.x && selectedTile.y === t.y;

      // Fog-of-war is always rendered (based on the latest observer results),
      // but observers are only refreshed while the Vision tool is active.
      const isObserved = observedTileKeys.has(`${t.x},${t.y}`);

      const selectionOverlay = isSelected ? "linear-gradient(rgba(255, 255, 0, 0.35), rgba(255, 255, 0, 0.35)), " : "";

      const fogOverlay = !isObserved ? "linear-gradient(rgba(80, 80, 80, 0.55), rgba(80, 80, 80, 0.55)), " : "";

      return {
        background: `${selectionOverlay}${fogOverlay}${baseBg}`,
        outline: isSelected ? "2px solid rgba(255, 255, 0, 0.8)" : undefined,
        outlineOffset: "-2px",
      };
    },
    [biomeColorByType, selectedTile, observedTileKeys],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";

      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";

      // If biomeName is "Unknown" and GeographyName is "Unknown" then just show "Unknown" once.
      const label = biomeName === "Unknown" && geographyName === "Unknown" ? "Unknown" : `${biomeName}, ${geographyName}`;

      return `(${t.x}, ${t.y}); ${label}`;
    },
    [biomeNameByType, geographyNameByType],
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
                geographies={geographies ?? []}
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
