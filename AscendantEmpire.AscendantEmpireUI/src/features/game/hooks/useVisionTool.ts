import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useClearPlayerCampaignMapMutation, useLoadPlayerCampaignMapMutation } from "@/features/map/hooks/useCampaignMapTiles";
import type { LoadPlayerCampaignMapRequest, PlayerCampaignMapObserverResultDto } from "@/features/map/api/playerCampaignMapApi";
import type { ToastVariant } from "@/hooks/useToast";

type VisionActiveTool = "vision" | null;
type SelectedTile = { x: number; y: number };

interface UseVisionToolOptions {
  gameId: number | undefined;
  playerId: number | undefined;
  openToast: (message: string, variant?: ToastVariant) => void;
}

/**
 * Owns the "check vision" dev tool: the active state, the selected tile, the observer
 * results (and the derived set of observed tile keys), the vision settings, and the
 * request/clear mutations. Requests are de-duplicated via refs so toggling the tool or
 * re-selecting the same tile doesn't refire an identical load.
 */
export function useVisionTool({ gameId, playerId, openToast }: UseVisionToolOptions) {
  const [activeTool, setActiveTool] = useState<VisionActiveTool>(null);
  const [selectedTile, setSelectedTile] = useState<SelectedTile | null>(null);
  const [visionObservers, setVisionObservers] = useState<PlayerCampaignMapObserverResultDto[]>([]);
  const [showVisionSettings, setShowVisionSettings] = useState(false);
  const [visionRadius, setVisionRadius] = useState<number>(0);
  const [visionLoadWholeMap, setVisionLoadWholeMap] = useState<boolean>(false);

  const visionLastRequestKeyRef = useRef<string | null>(null);
  const visionInFlightRef = useRef(false);
  const visionMutateRef = useRef<null | ((payload: LoadPlayerCampaignMapRequest) => void)>(null);
  const prevActiveToolRef = useRef<VisionActiveTool>(null);

  // Reset vision state when the game closes.
  useEffect(() => {
    if (gameId == null) {
      setActiveTool(null);
      setSelectedTile(null);
      setVisionObservers([]);
    }
  }, [gameId]);

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

  // Re-send the last vision request when the tool transitions into "vision".
  useEffect(() => {
    const prev = prevActiveToolRef.current;
    prevActiveToolRef.current = activeTool;

    if (activeTool !== "vision") return;
    if (prev === "vision") return;
    if (!selectedTile) return;

    sendVisionRequest(selectedTile.x, selectedTile.y);
  }, [activeTool, selectedTile, sendVisionRequest]);

  return {
    activeTool,
    selectedTile,
    setSelectedTile,
    observedTileKeys,
    visionRadius,
    setVisionRadius,
    visionLoadWholeMap,
    setVisionLoadWholeMap,
    showVisionSettings,
    setShowVisionSettings,
    toggleVisionTool,
    openVisionSettings,
    clearVision,
    sendVisionRequest,
  };
}
