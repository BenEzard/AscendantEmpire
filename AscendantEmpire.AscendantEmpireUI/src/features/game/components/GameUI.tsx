import type { ToolBarGroup } from "@/components/Toolbar";
import ToolBar from "@/components/Toolbar";
import ResourceTickerCollection, { type ResourceCategoryData } from "@/features/game/resources/ResourceTickerCollection";
import {
  useClearPlayerCampaignMapMutation,
  useLoadPlayerCampaignMapMutation,
  usePlayerCampaignMapTilesQuery,
} from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid from "@/features/map/components/TileGrid";
import type { TileCell } from "@/features/map/components/TileTypes";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import type { LoadPlayerCampaignMapRequest, PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { Alert, Spinner, Toast, ToastViewport } from "@/components/ui";
import { FaChessKnight, FaRegFolderOpen } from "react-icons/fa6";
import { GiClayBrick, GiStoneBlock, GiWoodStick } from "react-icons/gi";
import { GoEye, GoEyeClosed } from "react-icons/go";
import { getResourceIconByLabel } from "@/features/game/resources/resourceIcons";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";
import OpenGameDialog from "./OpenGameDialog";
import VisionSettingsOffcanvas from "./VisionSettingsOffcanvas";
import type { OpenGameSelection } from "../types/OpenGameSelection";
import { GrCaretNext } from "react-icons/gr";
import { http } from "@/api/http";

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
  type ActivePanel = "unit-design" | null;

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [activeTool, setActiveTool] = useState<ActiveTool>(null);
  const [selectedTile, setSelectedTile] = useState<SelectedTile | null>(null);
  const [visionObservers, setVisionObservers] = useState<PlayerCampaignMapObserverResultDto[]>([]);
  const [showVisionSettings, setShowVisionSettings] = useState(false);
  const [visionRadius, setVisionRadius] = useState<number>(0);
  const [visionLoadWholeMap, setVisionLoadWholeMap] = useState<boolean>(false);
  const [showConstructionPanel, setShowConstructionPanel] = useState(false);
  const [activePanel, setActivePanel] = useState<ActivePanel>(null);
  const [constructionOverlayTop, setConstructionOverlayTop] = useState<number>(0);
  const [constructionOverlayLeft, setConstructionOverlayLeft] = useState<number>(0);
  const constructionOverlayRef = useRef<HTMLDivElement | null>(null);

  const constructionButtonDomId = "toolbar-btn-construction-material";

  const visionLastRequestKeyRef = useRef<string | null>(null);
  const visionInFlightRef = useRef(false);
  const visionMutateRef = useRef<null | ((payload: LoadPlayerCampaignMapRequest) => void)>(null);
  const prevActiveToolRef = useRef<ActiveTool>(null);

  const [gameSelection, setGameSelection] = useState<OpenGameSelection | null>(null);

  const gameGuid = gameSelection?.gameGuid;
  const playerGuid = gameSelection?.playerGuid;
  const tilesEnabled = !!gameGuid && !!playerGuid;

  useEffect(() => {
    if (gameSelection == null) {
      setActiveTool(null);
      setSelectedTile(null);
      setVisionObservers([]);
    }
  }, [gameSelection]);

  // Tile selection should ONLY exist while Vision is active.
  useEffect(() => {
    if (activeTool !== "vision") setSelectedTile(null);
  }, [activeTool]);

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

  const openVisionSettings = useCallback(() => {
    setShowVisionSettings(true);
  }, []);

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
    setActiveTool(null);
    setVisionObservers([]);

    if (!gameGuid || !playerGuid) return;
    clearPlayerCampaignMap.mutate({ gameGuid, playerGuid });
  }, [clearPlayerCampaignMap, gameGuid, playerGuid]);

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
          onMiddleClick: openVisionSettings,
          middleClickHint: "settings",
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

  const secondaryToolBarGroups: ToolBarGroup[] = [
    {
      caption: "Structures",
      buttons: [
        {
          key: "wood-stick",
          icon: <GiWoodStick />,
          tooltip: "Construction Material",
          domId: constructionButtonDomId,
          onClick: () => setShowConstructionPanel((v) => !v),
        },
        {
          key: "unit-design",
          icon: <FaChessKnight />,
          tooltip: "Unit Design",
          active: activePanel === "unit-design",
          onClick: () => setActivePanel((p) => (p === "unit-design" ? null : "unit-design")),
        },
        {
          key: "end-turn",
          icon: <GrCaretNext />,
          tooltip: "End Turn",
          onClick: async () => {
            const payload = {
              gameGuid: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
              playerGuid: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            };

            try {
              await http.post("/turn/end", payload);

              // optional: only toggle after success
              setActivePanel((p) => (p === "unit-design" ? null : "unit-design"));
            } catch (err) {
              console.error("POST /turn/end failed", err);
              // optional toast/snackbar here
            }
          },
        },
      ],
    },
  ];

  const [constructionCategories, setConstructionCategories] = useState<ResourceCategoryData[]>(() => {
    // Dummy data: Totals are computed as the left-most ticker.
    const woodByType: { label: string; amount: number; isLuxury?: boolean }[] = [
      { label: "Oak", amount: 1200 },
      { label: "Generic Wood", amount: 2400 },
      { label: "Cedar", amount: 999 },
      { label: "Ash", amount: 250 },
      { label: "Elm", amount: 99 },
      { label: "Ebony", amount: 12, isLuxury: true },
    ];
    const stoneByType: { label: string; amount: number; isLuxury?: boolean }[] = [
      { label: "Limestone", amount: 10_000 },
      { label: "Sandstone", amount: 15_500 },
      { label: "Granite", amount: 20_000 },
      { label: "Basalt", amount: 45_000 },
      { label: "Generic Rock", amount: 75_000 },
      { label: "Marble", amount: 3_500, isLuxury: true },
      { label: "Flint", amount: 1_200 },
      { label: "Obsidian", amount: 420, isLuxury: true },
      { label: "Slate", amount: 8_000 },
    ];
    const otherByType: { label: string; amount: number }[] = [
      { label: "Bone", amount: 500 },
      { label: "Fodder", amount: 42 },
      { label: "Ochre", amount: 650 },
      { label: "Chalk", amount: 2_250 },
    ];

    const iconColorPalette = ["var(--bs-primary)", "var(--bs-success)", "var(--bs-warning)", "var(--bs-danger)", "var(--bs-info)"];

    const randomIconColor = () => {
      const idx = Math.floor(Math.random() * iconColorPalette.length);
      return iconColorPalette[idx] ?? "var(--bs-primary)";
    };

    const otherTickers = otherByType.map((t, idx) => ({
      id: `other-${t.label.toLowerCase().replaceAll(" ", "-")}`,
      label: t.label,
      column: idx + 1,
      amount: t.amount,
      iconColor: randomIconColor(),
      icon: getResourceIconByLabel(t.label),
    }));

    const woodTickers = woodByType.map((t, idx) => ({
      id: `wood-${t.label.toLowerCase().replaceAll(" ", "-")}`,
      label: t.label,
      column: idx + 1,
      amount: t.amount,
      isLuxury: t.isLuxury === true,
      iconColor: randomIconColor(),
      icon: getResourceIconByLabel(t.label),
    }));

    const stoneTickers = stoneByType.map((t, idx) => ({
      id: `stone-${t.label.toLowerCase().replaceAll(" ", "-")}`,
      label: t.label,
      column: idx + 1,
      amount: t.amount,
      isLuxury: t.isLuxury === true,
      iconColor: randomIconColor(),
      icon: getResourceIconByLabel(t.label),
    }));

    return [
      { key: "wood", label: "Wood", column: 1, icon: <GiWoodStick />, tickers: woodTickers },
      {
        key: "stone",
        label: "Stone",
        column: 2,
        icon: <GiStoneBlock />,
        tickers: stoneTickers,
      },
      { key: "other", label: "Other", column: 3, icon: <GiClayBrick />, tickers: otherTickers, mixedItems: true },
    ];
  });

  useEffect(() => {
    if (!showConstructionPanel) return;

    const computePosition = () => {
      const buttonEl = document.getElementById(constructionButtonDomId);
      if (!buttonEl) return;
      const rect = buttonEl.getBoundingClientRect();

      // fixed overlay; rect is viewport-relative
      setConstructionOverlayTop(Math.round(rect.bottom + 6));
      setConstructionOverlayLeft(Math.round(rect.left));
    };

    computePosition();
    window.addEventListener("resize", computePosition);
    window.addEventListener("scroll", computePosition, { passive: true });
    return () => {
      window.removeEventListener("resize", computePosition);
      window.removeEventListener("scroll", computePosition);
    };
  }, [showConstructionPanel, constructionButtonDomId]);

  // Close the construction popup when clicking outside of it.
  useEffect(() => {
    if (!showConstructionPanel) return;

    const onPointerDown = (e: PointerEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;

      const overlayEl = constructionOverlayRef.current;
      if (overlayEl && overlayEl.contains(target)) return;

      const buttonEl = document.getElementById(constructionButtonDomId);
      if (buttonEl && buttonEl.contains(target)) return;

      setShowConstructionPanel(false);
    };

    // Capture phase so we close even if inner elements stop propagation.
    document.addEventListener("pointerdown", onPointerDown, true);
    return () => document.removeEventListener("pointerdown", onPointerDown, true);
  }, [showConstructionPanel, constructionButtonDomId]);

  const { data: biomes } = useCampaignMapBiomesQuery();
  const { data: geographies } = useCampaignMapGeographiesQuery();

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
    for (const g of geographies ?? []) m.set(g.campaignMapGeographyType, g.name);
    return m;
  }, [geographies]);

  const {
    data: tilesResponse,
    error: tilesError,
    isLoading: tilesLoading,
    isFetching: tilesFetching,
  } = usePlayerCampaignMapTilesQuery(gameGuid, playerGuid, tilesEnabled);

  const loadPlayerCampaignMap = useLoadPlayerCampaignMapMutation({
    onSuccess: (data) => {
      visionInFlightRef.current = false;
      setVisionObservers(data.observers ?? []);
    },
    onError: (error) => {
      visionInFlightRef.current = false;
      const msg = error instanceof Error ? error.message : "Unknown error";
      openToast(`Vision load failed: ${msg}`, "danger");
    },
  });

  useEffect(() => {
    visionMutateRef.current = loadPlayerCampaignMap.mutate;
  }, [loadPlayerCampaignMap.mutate]);

  const sendVisionRequest = useCallback(
    (x: number, y: number, _source: "tile-click" | "vision-enabled") => {
      if (activeTool !== "vision") return;
      if (!gameGuid || !playerGuid) return;

      const requestKeyWithRadius = `${gameGuid}:${playerGuid}:${x},${y}:r${visionRadius}`;
      const requestKey = `${requestKeyWithRadius}:whole${visionLoadWholeMap ? 1 : 0}`;

      if (visionInFlightRef.current && visionLastRequestKeyRef.current === requestKey) return;
      if (visionLastRequestKeyRef.current === requestKey) return;

      const payload: LoadPlayerCampaignMapRequest = {
        gameGuid,
        playerGuid,
        loadWholeMap: visionLoadWholeMap,
      };

      visionLastRequestKeyRef.current = requestKey;
      visionInFlightRef.current = true;

      const mutate = visionMutateRef.current;
      if (!mutate) {
        visionInFlightRef.current = false;
        return;
      }

      mutate(payload);
    },
    [activeTool, gameGuid, playerGuid, visionRadius, visionLoadWholeMap],
  );

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;
    return buildTiles2d(gameSelection.width, gameSelection.height, tilesResponse.tiles);
  }, [gameSelection, tilesResponse]);

  const onTileClick = useCallback(
    (t: TileCell) => {
      if (activeTool === "vision") {
        setSelectedTile({ x: t.x, y: t.y });
        sendVisionRequest(t.x, t.y, "tile-click");
      }

      const biomeName = t.biomeTypeId == null ? "null" : (biomeNameByType.get(t.biomeTypeId) ?? `unknown(${t.biomeTypeId})`);
      const geoName = t.geographyTypeId == null ? "null" : (geographyNameByType.get(t.geographyTypeId) ?? `unknown(${t.geographyTypeId})`);
      const label = biomeName === "Unknown" && geoName === "Unknown" ? "Unknown" : `${biomeName}, ${geoName}`;

      openToast(`[${t.x}, ${t.y}]: ${label}`, "warning");
    },
    [activeTool, biomeNameByType, geographyNameByType, openToast, sendVisionRequest],
  );

  useEffect(() => {
    const prev = prevActiveToolRef.current;
    prevActiveToolRef.current = activeTool;

    if (activeTool !== "vision") return;
    if (prev === "vision") return;
    if (!selectedTile) return;

    sendVisionRequest(selectedTile.x, selectedTile.y, "vision-enabled");
  }, [activeTool, selectedTile, sendVisionRequest]);

  const getTileStyle = useCallback(
    (t: TileCell): CSSProperties => {
      const bg = t.biomeTypeId != null ? biomeColorByType.get(t.biomeTypeId) : undefined;
      const baseBg = bg ?? "rgba(0,0,0,0.03)";

      const isSelected = selectedTile != null && selectedTile.x === t.x && selectedTile.y === t.y;
      const isObserved = observedTileKeys.has(`${t.x},${t.y}`);

      const selectionOverlay = isSelected ? "linear-gradient(rgba(255, 255, 0, 0.35), rgba(255, 255, 0, 0.35)), " : "";
      const fogOverlay = !isObserved ? "linear-gradient(rgba(80, 80, 80, 0.55), rgba(80, 80, 80, 0.55)), " : "";

      return {
        background: `${selectionOverlay}${fogOverlay}${baseBg}`,
        outline: isSelected ? "2px solid rgba(255, 255, 0, 0.8)" : undefined,
        outlineOffset: "-2px",
      };
    },
    [biomeColorByType, observedTileKeys, selectedTile],
  );

  const getTileTitle = useCallback(
    (t: TileCell) => {
      const biomeName = t.biomeTypeId != null ? (biomeNameByType.get(t.biomeTypeId) ?? "(unknown biome)") : "(no biome)";
      const geographyName = t.geographyTypeId != null ? (geographyNameByType.get(t.geographyTypeId) ?? "(unknown geography)") : "(no geography)";
      const label = biomeName === "Unknown" && geographyName === "Unknown" ? "Unknown" : `${biomeName}, ${geographyName}`;
      return `(${t.x}, ${t.y}); ${label}`;
    },
    [biomeNameByType, geographyNameByType],
  );

  return (
    <div className="flex flex-col" style={{ minHeight: "100vh" }}>
      <ToolBar groups={toolBarGroups} />
      <ToolBar
        groups={secondaryToolBarGroups}
        containerClassName="toolbar flex justify-start border-b border-slate-200 bg-[var(--bs-primary)]"
        showCaptions={false}
        buttonVariant="light"
      />

      {showConstructionPanel && (
        <div
          ref={constructionOverlayRef}
          style={{
            position: "fixed",
            top: constructionOverlayTop,
            left: constructionOverlayLeft,
            transform: undefined,
            zIndex: 1050,
            background: "rgba(255, 255, 255, 0.96)",
            border: "1px solid rgba(0, 0, 0, 0.12)",
            borderRadius: "8px",
            boxShadow: "0 10px 30px rgba(0,0,0,0.2)",
            padding: "0.5rem",
            width: "fit-content",
            maxWidth: "calc(100vw - 2rem)",
          }}>
          <ResourceTickerCollection
            categories={constructionCategories}
            onReorderCategoryTickers={({ categoryKey, tickers }) => {
              setConstructionCategories((prev) => prev.map((c) => (c.key === categoryKey ? { ...c, tickers } : c)));
            }}
            onClickAmount={({ label, amount, isTotal }) => {
              const prefix = isTotal ? "Total" : "Slot";
              openToast(`${prefix} ${label}: ${amount}`, "info");
            }}
          />
        </div>
      )}

      <VisionSettingsOffcanvas
        show={showVisionSettings}
        onHide={() => setShowVisionSettings(false)}
        visionRadius={visionRadius}
        setVisionRadius={setVisionRadius}
        visionLoadWholeMap={visionLoadWholeMap}
        setVisionLoadWholeMap={setVisionLoadWholeMap}
      />

      <OpenGameDialog
        show={activeDialog === "openGame"}
        onClose={() => setActiveDialog(null)}
        onSaveSuccess={(msg) => openToast(msg ?? "Undefined", "success")}
        onOpenMap={(selection) => {
          setGameSelection(selection);
          setActiveDialog(null);
        }}
      />

      <ToastViewport>
        <Toast open={toast.show} onClose={closeToast} variant={toast.variant} message={toast.message} autoHideMs={3000} />
      </ToastViewport>

      <div className="flex flex-col grow min-h-0">
        {activePanel === "unit-design" ? (
          <UnitDesigner onClose={() => setActivePanel(null)} />
        ) : (
          <div className="mt-3">
            {gameSelection == null ? (
              <Alert variant="secondary" className="mb-0">
                No game opened. Use <strong>Game → Open</strong>.
              </Alert>
            ) : (
              <>
                <div className="flex items-center justify-between mb-2">
                  {(tilesLoading || tilesFetching) && (
                    <div className="flex items-center gap-2 text-slate-500">
                      <Spinner size="sm" />
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
        )}
      </div>
    </div>
  );
}

export default GameUI;
