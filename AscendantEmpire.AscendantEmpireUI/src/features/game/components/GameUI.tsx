import type { ToolBarGroup } from "@/components/Toolbar";
import ToolBar from "@/components/Toolbar";
import ResourceTickerCollection, {
  type ResourceCategoryData,
  type ResourceTickerData,
} from "@/features/game/resources/tickers/ResourceTickerCollection";
import ResourceManagerDialog from "@/features/game/resources/manager/ResourceManagerDialog";
import { resourceDummyData } from "../resources/tickers/ResourceDummyData";
import {
  useClearPlayerCampaignMapMutation,
  useLoadPlayerCampaignMapMutation,
  usePlayerCampaignMapTilesQuery,
  playerCampaignTilesKey,
} from "@/features/map/hooks/useCampaignMapTiles";
import TileGrid, { TILE_SIZE_PX } from "@/features/map/components/TileGrid";
import type { ObservedTroopTile, TileCell } from "@/features/map/components/TileTypes";
import { useCampaignMapBiomesQuery } from "@/features/map/hooks/useCampaignMapBiomes";
import { useCampaignMapGeographiesQuery } from "@/features/map/hooks/useCampaignMapGeographies";
import { useTroopDisplayInfoQuery } from "@/features/map/hooks/useTroopDisplayInfo";
import type { CampaignMapTileDto, LoadPlayerCampaignMapRequest, PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";
import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { Alert, HamburgerMenu, Spinner, Toast, ToastViewport } from "@/components/ui";
import { FaChessKnight, FaRegFolderOpen } from "react-icons/fa6";
import { GiHuntingHorn, GiWoodStick } from "react-icons/gi";
import { GoEye, GoEyeClosed } from "react-icons/go";
import UnitDesigner from "@/features/unitdesign/UnitDesigner";
import OpenGameDialog from "./OpenGameDialog";
import VisionSettingsOffcanvas from "./VisionSettingsOffcanvas";
import type { OpenGameSelection } from "../types/OpenGameSelection";
import { GrCaretNext } from "react-icons/gr";
import { SlClose } from "react-icons/sl";
import { http } from "@/api/http";
import { useQueryClient } from "@tanstack/react-query";
import ContextActionToolbar, { type ContextActionToolbarAction } from "./ContextActionToolbar";
import ForceSelectionDialog, { type ForceSelectionDialogTile } from "./ForceSelectionDialog";
import { VscNewFile } from "react-icons/vsc";
import NotificationsHost from "@/features/notifications/NotificationsHost";
import { NotificationsProvider } from "@/features/notifications/useNotifications";

function buildTiles2d(width: number, height: number, tiles: CampaignMapTileDto[]): TileCell[][] {
  const grid: TileCell[][] = Array.from({ length: height }, (_, y) =>
    Array.from({ length: width }, (_, x) => ({
      x,
      y,
      biomeTypeId: null,
      geographyTypeId: null,
      structures: undefined,
      troops: undefined,
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
      structures: t.structures,
      troops: t.troops,
    };
  }

  return grid;
}

function getTileKey(x: number, y: number): string {
  return `${x},${y}`;
}

/// Returns the troop with the highest number of healthy troops. Returns null if the input array is empty.
/// (they will be the default display-on-tile)
function getTroopWithMaxHealthy(troops: ObservedTroopTile[]): ObservedTroopTile | null {
  if (troops.length === 0) return null;

  let strongestTroop = troops[0];

  for (const troop of troops.slice(1)) {
    if (troop.healthyTroops > strongestTroop.healthyTroops) {
      strongestTroop = troop;
    }
  }

  return strongestTroop;
}

function GameUI({ topOffset = 0 }: { topOffset?: number }) {
  type ActiveDialog = "openGame" | null;
  type ActiveTool = "vision" | null;
  type SelectedTile = { x: number; y: number };
  type ActivePanel = "unit-design" | null;

  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [activeTool, setActiveTool] = useState<ActiveTool>(null);
  const [selectedTile, setSelectedTile] = useState<SelectedTile | null>(null);
  const [selectedForce, setSelectedForce] = useState<ObservedTroopTile | null>(null);
  const [visibleTroopIdsByTile, setVisibleTroopIdsByTile] = useState<Record<string, number>>({});
  const [pendingForceSelectionTile, setPendingForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);
  const [forceSelectionTile, setForceSelectionTile] = useState<ForceSelectionDialogTile | null>(null);
  const [visionObservers, setVisionObservers] = useState<PlayerCampaignMapObserverResultDto[]>([]);
  const [showVisionSettings, setShowVisionSettings] = useState(false);
  const [visionRadius, setVisionRadius] = useState<number>(0);
  const [visionLoadWholeMap, setVisionLoadWholeMap] = useState<boolean>(false);
  const [showConstructionPanel, setShowConstructionPanel] = useState(false);
  const [activePanel, setActivePanel] = useState<ActivePanel>(null);
  const [constructionOverlayTop, setConstructionOverlayTop] = useState<number>(0);
  const [constructionOverlayLeft, setConstructionOverlayLeft] = useState<number>(0);
  const constructionOverlayRef = useRef<HTMLDivElement | null>(null);
  const toolbarRef = useRef<HTMLDivElement | null>(null);
  const [toolbarBottomPx, setToolbarBottomPx] = useState<number>(0);
  const tileGridContainerRef = useRef<HTMLDivElement | null>(null);

  const constructionButtonDomId = "toolbar-btn-construction-material";

  const visionLastRequestKeyRef = useRef<string | null>(null);
  const visionInFlightRef = useRef(false);
  const visionMutateRef = useRef<null | ((payload: LoadPlayerCampaignMapRequest) => void)>(null);
  const prevActiveToolRef = useRef<ActiveTool>(null);

  const [gameSelection, setGameSelection] = useState<OpenGameSelection | null>(null);

  const queryClient = useQueryClient();

  const gameId = gameSelection?.gameId;
  const playerId = gameSelection?.playerId;
  const tilesEnabled = gameId != null && playerId != null;

  useEffect(() => {
    if (gameSelection == null) {
      setActiveTool(null);
      setSelectedTile(null);
      setSelectedForce(null);
      setVisibleTroopIdsByTile({});
      setPendingForceSelectionTile(null);
      setForceSelectionTile(null);
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

  useEffect(() => {
    const compute = () => {
      const el = toolbarRef.current;
      if (!el) return;
      const rect = el.getBoundingClientRect();
      setToolbarBottomPx(Math.round(rect.bottom));
    };

    compute();
    window.addEventListener("resize", compute);
    window.addEventListener("scroll", compute, { passive: true });

    const el = toolbarRef.current;
    if (!el || !("ResizeObserver" in window)) {
      return () => {
        window.removeEventListener("resize", compute);
        window.removeEventListener("scroll", compute);
      };
    }

    const ro = new ResizeObserver(compute);
    ro.observe(el);

    return () => {
      ro.disconnect();
      window.removeEventListener("resize", compute);
      window.removeEventListener("scroll", compute);
    };
  }, []);

  const closeToast = () => setToast((t) => ({ ...t, show: false }));

  const panMapTo = useCallback((x: number, y: number) => {
    const el = tileGridContainerRef.current;
    if (!el) return;

    const tileCenterX = x * TILE_SIZE_PX + TILE_SIZE_PX / 2;
    const tileCenterY = y * TILE_SIZE_PX + TILE_SIZE_PX / 2;

    const hasInternalHScroll = el.scrollWidth > el.clientWidth;
    const hasInternalVScroll = el.scrollHeight > el.clientHeight;

    if (hasInternalHScroll || hasInternalVScroll) {
      // Container is height/width-constrained and scrolls internally.
      const maxLeft = Math.max(0, el.scrollWidth - el.clientWidth);
      const maxTop = Math.max(0, el.scrollHeight - el.clientHeight);
      el.scrollTo({
        left: Math.max(0, Math.min(maxLeft, tileCenterX - el.clientWidth / 2)),
        top: Math.max(0, Math.min(maxTop, tileCenterY - el.clientHeight / 2)),
        behavior: "smooth",
      });
    } else {
      // Container expands to its content (no internal scroll) — the window scrolls instead.
      const rect = el.getBoundingClientRect();
      const absLeft = rect.left + window.scrollX + tileCenterX;
      const absTop = rect.top + window.scrollY + tileCenterY;
      window.scrollTo({
        left: Math.max(0, absLeft - window.innerWidth / 2),
        top: Math.max(0, absTop - window.innerHeight / 2),
        behavior: "smooth",
      });
    }
  }, []);

  const openNotificationDialog = useCallback(
    (dialog: "unit-orders" | "trade" | "city", _payload?: unknown) => {
      openToast(`Open dialog: ${dialog}`, "info");
    },
    [openToast],
  );

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

    if (gameId == null || playerId == null) return;
    clearPlayerCampaignMap.mutate({ gameId, playerId });
  }, [clearPlayerCampaignMap, gameId, playerId]);

  const closeGame = useCallback(() => {
    if (gameId != null && playerId != null) {
      const key = playerCampaignTilesKey(gameId, playerId);
      queryClient.cancelQueries({ queryKey: key });
      queryClient.removeQueries({ queryKey: key });
    }

    setGameSelection(null);
    setActiveDialog(null);
    setSelectedForce(null);
    setVisibleTroopIdsByTile({});
    setPendingForceSelectionTile(null);
    setForceSelectionTile(null);
  }, [gameId, playerId, queryClient]);

  const toolBarGroups: ToolBarGroup[] = [
    {
      caption: "Game",
      buttons: [
        {
          key: "game-menu",
          render: () => (
            <HamburgerMenu
              title="Game menu"
              tooltip="Game"
              groups={[
                {
                  label: "Game",
                  items: [
                    {
                      key: "new",
                      icon: <VscNewFile />,
                      label: "New",
                      tooltip: "New Game",
                      onClick: () => {},
                    },
                    {
                      key: "open",
                      icon: <FaRegFolderOpen />,
                      label: "Open",
                      tooltip: "Open Game",
                      onClick: () => setActiveDialog("openGame"),
                    },
                    {
                      key: "close",
                      icon: <SlClose />,
                      label: "Close",
                      tooltip: "Close Game",
                      disabled: gameSelection == null,
                      onClick: closeGame,
                    },
                  ],
                },
                {
                  label: "Dev Tools",
                  items: [
                    {
                      key: "clear-vision",
                      icon: <GoEyeClosed />,
                      label: "Clear Vision",
                      tooltip: "Clear Vision",
                      disabled: gameSelection == null,
                      onClick: clearVision,
                    },
                  ],
                },
              ]}
            />
          ),
        },
      ],
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
      ],
    },
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
            if (gameId == null || playerId == null) return;

            const payload = {
              gameId,
              playerId,
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

  const [constructionCategories, setConstructionCategories] = useState<ResourceCategoryData[]>(() => resourceDummyData);

  const [showResourceManager, setShowResourceManager] = useState(false);
  const [resourceManagerSelectedId, setResourceManagerSelectedId] = useState<string | null>(null);

  const allConstructionTickers = useMemo<ResourceTickerData[]>(() => {
    const result: ResourceTickerData[] = [];
    for (const category of constructionCategories) {
      if (category.type === "processed-resources") {
        for (const group of category.tickerGroups) {
          result.push(...group.tickers);
        }
      } else if (category.type === "standalone-resources") {
        result.push(...category.resources);
      } else if (category.type === "manufactured-grades") {
        for (const collection of category.collections) {
          result.push(...Object.values(collection.resourcesByGrade));
        }
      }
    }
    return result;
  }, [constructionCategories]);

  useEffect(() => {
    if (!showConstructionPanel) return;

    const computePosition = () => {
      const buttonEl = document.getElementById(constructionButtonDomId);
      if (!buttonEl) return;
      const rect = buttonEl.getBoundingClientRect();

      // fixed overlay; rect is viewport-relative
      setConstructionOverlayTop(Math.round(rect.bottom + 20));
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
  } = usePlayerCampaignMapTilesQuery(gameId, playerId, tilesEnabled);
  const { data: troopImageSrcByUnitDesignId } = useTroopDisplayInfoQuery(gameId, gameId != null);

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
    (x: number, y: number) => {
      if (activeTool !== "vision") return;
      if (gameId == null || playerId == null) return;

      const requestKeyWithRadius = `${gameId}:${playerId}:${x},${y}:r${visionRadius}`;
      const requestKey = `${requestKeyWithRadius}:whole${visionLoadWholeMap ? 1 : 0}`;

      if (visionInFlightRef.current && visionLastRequestKeyRef.current === requestKey) return;
      if (visionLastRequestKeyRef.current === requestKey) return;

      const payload: LoadPlayerCampaignMapRequest = {
        gameId,
        playerId,
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
    [activeTool, gameId, playerId, visionRadius, visionLoadWholeMap],
  );

  const tiles2d = useMemo(() => {
    if (!gameSelection || !tilesResponse) return null;
    return buildTiles2d(gameSelection.width, gameSelection.height, tilesResponse.tiles);
  }, [gameSelection, tilesResponse]);

  const getVisibleTroopForTile = useCallback(
    (tile: TileCell, options?: { troops?: ObservedTroopTile[] }) => {
      const troops = options?.troops ?? tile.troops ?? [];
      if (troops.length === 0) return null;

      const visibleTroopId = visibleTroopIdsByTile[getTileKey(tile.x, tile.y)];
      if (visibleTroopId != null) {
        const visibleTroop = troops.find((troop) => troop.id === visibleTroopId);
        if (visibleTroop) return visibleTroop;
      }

      return getTroopWithMaxHealthy(troops);
    },
    [visibleTroopIdsByTile],
  );

  const getVisibleTroopId = useCallback((tile: TileCell) => getVisibleTroopForTile(tile)?.id, [getVisibleTroopForTile]);

  const closeForceSelectionDialog = useCallback(() => {
    setForceSelectionTile(null);
  }, []);

  const openForceSelectionDialog = useCallback(() => {
    if (pendingForceSelectionTile == null) return;
    setForceSelectionTile(pendingForceSelectionTile);
  }, [pendingForceSelectionTile]);

  const chooseForce = useCallback(
    (troop: ObservedTroopTile) => {
      setSelectedForce(troop);
      if (forceSelectionTile) {
        setVisibleTroopIdsByTile((current) => ({
          ...current,
          [getTileKey(forceSelectionTile.x, forceSelectionTile.y)]: troop.id,
        }));
        setPendingForceSelectionTile(forceSelectionTile);
      }
      setForceSelectionTile(null);
    },
    [forceSelectionTile],
  );

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() !== "t") return;
      if (pendingForceSelectionTile == null) return;

      const target = event.target;
      if (target instanceof HTMLElement) {
        const tagName = target.tagName;
        const isEditable = target.isContentEditable || tagName === "INPUT" || tagName === "TEXTAREA" || tagName === "SELECT";
        if (isEditable) return;
      }

      setForceSelectionTile(pendingForceSelectionTile);
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [pendingForceSelectionTile]);

  const moveTo = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;

      openToast(`Move troop #${selectedForce.id} to [${tile.x}, ${tile.y}]`, "info");
    },
    [openToast, selectedForce],
  );

  const hunt = useCallback(() => {
    if (!selectedForce) return;

    openToast(`Hunt with troop #${selectedForce.id}`, "info");
  }, [openToast, selectedForce]);

  const onTileClick = useCallback(
    (t: TileCell) => {
      if (activeTool === "vision") {
        setSelectedTile({ x: t.x, y: t.y });
        sendVisionRequest(t.x, t.y);
      }

      if (playerId != null) {
        const ownedTroops = (t.troops ?? []).filter((troop) => troop.commanderId === playerId);

        if (ownedTroops.length === 1) {
          setSelectedForce(ownedTroops[0]);
          setVisibleTroopIdsByTile((current) => ({
            ...current,
            [getTileKey(t.x, t.y)]: ownedTroops[0].id,
          }));
          setPendingForceSelectionTile(null);
          setForceSelectionTile(null);
        } else if (ownedTroops.length > 1) {
          const displayedOwnedTroop = getVisibleTroopForTile(t, { troops: ownedTroops }) ?? ownedTroops[0];

          setSelectedForce(displayedOwnedTroop);
          setVisibleTroopIdsByTile((current) => ({
            ...current,
            [getTileKey(t.x, t.y)]: displayedOwnedTroop.id,
          }));
          setPendingForceSelectionTile({ x: t.x, y: t.y, troops: ownedTroops });
          setForceSelectionTile(null);
        } else {
          setSelectedForce(null);
          setPendingForceSelectionTile(null);
          setForceSelectionTile(null);
        }
      }

      const biomeName = t.biomeTypeId == null ? "null" : (biomeNameByType.get(t.biomeTypeId) ?? `unknown(${t.biomeTypeId})`);
      const geoName = t.geographyTypeId == null ? "null" : (geographyNameByType.get(t.geographyTypeId) ?? `unknown(${t.geographyTypeId})`);
      const label = biomeName === "Unknown" && geoName === "Unknown" ? "Unknown" : `${biomeName}, ${geoName}`;

      openToast(`[${t.x}, ${t.y}]: ${label}`, "warning");
    },
    [activeTool, biomeNameByType, geographyNameByType, getVisibleTroopForTile, openToast, playerId, sendVisionRequest],
  );

  const onTileHold = useCallback(
    (tile: TileCell) => {
      if (!selectedForce) return;
      if ((tile.troops ?? []).some((troop) => troop.id === selectedForce.id)) return;

      moveTo(tile);
    },
    [moveTo, selectedForce],
  );

  useEffect(() => {
    const prev = prevActiveToolRef.current;
    prevActiveToolRef.current = activeTool;

    if (activeTool !== "vision") return;
    if (prev === "vision") return;
    if (!selectedTile) return;

    sendVisionRequest(selectedTile.x, selectedTile.y);
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

  const contextActionToolbarActions = useMemo<ContextActionToolbarAction[]>(() => {
    if (!selectedForce) return [];

    const actions: ContextActionToolbarAction[] = [];
    const hasPendingMultiTroopSelection = pendingForceSelectionTile != null;

    if (hasPendingMultiTroopSelection) {
      actions.push({
        key: "select-troop",
        label: "Select Troop",
        icon: <FaChessKnight />,
        onClick: openForceSelectionDialog,
      });
    }

    if (!hasPendingMultiTroopSelection && selectedForce.unitDesignId === 2) {
      actions.push({
        key: "hunt",
        label: "Hunt",
        icon: <GiHuntingHorn />,
        onClick: hunt,
      });
    }

    return actions;
  }, [hunt, openForceSelectionDialog, pendingForceSelectionTile, selectedForce]);

  return (
    <NotificationsProvider>
      <div className="flex flex-col" style={{ minHeight: "100vh" }}>
        <div ref={toolbarRef} className="fixed left-0 right-0 z-[1070]" style={{ top: topOffset }}>
          <ToolBar groups={toolBarGroups} />
        </div>
        {/* Spacer so content doesn't slide under the fixed toolbar (toolbar height only, tab strip is handled by App) */}
        <div style={{ height: Math.max(0, toolbarBottomPx - topOffset) }} />

        <NotificationsHost toolbarBottomPx={toolbarBottomPx} onPanTo={panMapTo} onOpenDialog={openNotificationDialog} />

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
              maximumColumns={4}
              onReorderCategoryTickerGroups={({ categoryKey, tickerGroups }) => {
                setConstructionCategories((prev) =>
                  prev.map((category) =>
                    category.key === categoryKey && category.type === "processed-resources" ? { ...category, tickerGroups } : category,
                  ),
                );
              }}
              onClickAmount={({ resourceId, isTotal }) => {
                if (isTotal) return;
                setResourceManagerSelectedId(resourceId ?? null);
                setShowResourceManager(true);
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

        <ForceSelectionDialog
          open={forceSelectionTile != null}
          tile={forceSelectionTile}
          selectedTroopId={selectedForce?.id}
          troopImageSrcByUnitDesignId={troopImageSrcByUnitDesignId}
          onClose={closeForceSelectionDialog}
          onSelect={chooseForce}
        />

        <ResourceManagerDialog
          show={showResourceManager}
          onClose={() => setShowResourceManager(false)}
          tickers={allConstructionTickers}
          selectedResourceId={resourceManagerSelectedId}
          onSelectResource={setResourceManagerSelectedId}
        />

        <ContextActionToolbar visible={selectedForce != null && contextActionToolbarActions.length > 0} actions={contextActionToolbarActions} />

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
                      containerRef={tileGridContainerRef}
                      selectedTroopId={selectedForce?.id}
                      troopImageSrcByUnitDesignId={troopImageSrcByUnitDesignId}
                      getVisibleTroopId={getVisibleTroopId}
                      onTileClick={onTileClick}
                      onTileHold={onTileHold}
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
    </NotificationsProvider>
  );
}

export default GameUI;
