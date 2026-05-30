import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Modal, Spinner, Tooltip } from "@/components/ui";
import type { OpenGameSelection } from "../types/OpenGameSelection";
import type { OpenGamesResponse } from "../types/OpenGamesResponse";
import type { OpenGame } from "../types/OpenGame";
import { getOpenGames } from "../hooks/getOpenGames";

export interface OpenGameDialogProps {
  show: boolean;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
  onOpenMap?: (selection: OpenGameSelection) => void;
}

export default function OpenGameDialog({ show, onClose, onSaveSuccess, onOpenMap }: OpenGameDialogProps) {
  const [filter, setFilter] = useState("");
  const [selected, setSelected] = useState<{ gameId: number; playerId: number } | null>(null);

  const { data, isLoading, isFetching, error, refetch } = useQuery<OpenGamesResponse, Error>({
    queryKey: ["games", "open"],
    queryFn: getOpenGames,
    enabled: show,
    refetchOnWindowFocus: false,
  });

  const games = useMemo<OpenGame[]>(() => data?.games ?? [], [data]);

  useEffect(() => {
    if (!show) return;
    setFilter("");
    setSelected(null);
  }, [show]);

  const filteredGames = useMemo(() => {
    const f = filter.trim().toLowerCase();
    if (!f) return games;

    return games.filter((g) => {
      if (g.gameName.toLowerCase().includes(f)) return true;
      return g.players.some((p) => p.rulerLabel.toLowerCase().includes(f));
    });
  }, [games, filter]);

  const selectedSelection = useMemo<OpenGameSelection | null>(() => {
    if (!selected) return null;

    const g = games.find((x) => x.gameId === selected.gameId);
    if (!g) return null;

    const p = g.players.find((x) => x.playerId === selected.playerId);
    if (!p) return null;

    return {
      gameId: g.gameId,
      gameName: g.gameName,
      width: g.mapWidth,
      height: g.mapHeight,
      playerId: p.playerId,
      rulerLabel: p.rulerLabel,
      campaignOffsetId: p.campaignOffsetId,
    };
  }, [selected, games]);

  const handleOpen = () => {
    if (!selectedSelection) return;

    console.log("Opening selection:", selectedSelection);
    onOpenMap?.(selectedSelection);
    onSaveSuccess(`Opened "${selectedSelection.gameName}" as ${selectedSelection.rulerLabel}`);
    onClose();
  };

  return (
    <Modal
      open={show}
      onClose={onClose}
      title="Open Game"
      size="lg"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleOpen} disabled={!selectedSelection}>
            Open
          </Button>
        </>
      }>
      <div className="flex items-center gap-2 mb-3">
        <input
          className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
          placeholder="Filter by game or player..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        <Button variant="outline-secondary" onClick={() => refetch()} disabled={isLoading || isFetching}>
          Refresh
        </Button>
      </div>

      {error && (
        <Alert variant="danger" className="mb-3">
          Failed to load games: {error.message}
        </Alert>
      )}

      {isLoading ? (
        <div className="flex items-center gap-2 text-slate-600">
          <Spinner size="sm" />
          <span>Loading games...</span>
        </div>
      ) : (
        <div className="max-h-[420px] overflow-auto rounded-md border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-700">
              <tr>
                <th className="px-3 py-2" style={{ width: "35%" }}>
                  Game
                </th>
                <th className="px-3 py-2" style={{ width: 120 }}>
                  Size
                </th>
                <th className="px-3 py-2">Player</th>
              </tr>
            </thead>

            <tbody>
              {filteredGames.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-3 py-3 text-slate-500">
                    No games found.
                  </td>
                </tr>
              ) : (
                filteredGames.flatMap((g) => {
                  const players = g.players.length ? g.players : [{ playerId: 0, rulerLabel: "(no players)", campaignOffsetId: 0 }];

                  return players.map((p, idx) => {
                    const rowKey = `${g.gameId}-${p.playerId || "none"}-${idx}`;
                    const isSelected = p.playerId > 0 && selected?.gameId === g.gameId && selected?.playerId === p.playerId;

                    const onClick = () => {
                      if (p.playerId <= 0) return;
                      setSelected({ gameId: g.gameId, playerId: p.playerId });
                    };

                    const onDoubleClick = () => {
                      if (p.playerId <= 0) return;

                      setSelected({ gameId: g.gameId, playerId: p.playerId });
                      const selection: OpenGameSelection = {
                        gameId: g.gameId,
                        gameName: g.gameName,
                        width: g.mapWidth,
                        height: g.mapHeight,
                        playerId: p.playerId,
                        rulerLabel: p.rulerLabel,
                        campaignOffsetId: p.campaignOffsetId,
                      };

                      onOpenMap?.(selection);
                      onSaveSuccess(`Opened "${selection.gameName}" as ${selection.rulerLabel}`);
                      onClose();
                    };

                    return (
                      <tr
                        key={rowKey}
                        className={[
                          "border-t border-slate-100",
                          p.playerId > 0 ? "cursor-pointer hover:bg-slate-50" : "",
                          isSelected ? "bg-slate-100" : "",
                        ].join(" ")}
                        role={p.playerId > 0 ? "button" : undefined}
                        onClick={onClick}
                        onDoubleClick={onDoubleClick}>
                        {idx === 0 && (
                          <>
                            <td rowSpan={players.length} className="px-3 py-2 align-top">
                              <div className="font-semibold">{g.gameName}</div>
                              <div className="text-slate-500" style={{ fontSize: "0.85rem" }}>
                                Players: {g.numberOfPlayers} • ID {g.gameId}
                              </div>
                            </td>

                            <td rowSpan={players.length} className="px-3 py-2 align-top">
                              {g.mapWidth ?? "?"}×{g.mapHeight ?? "?"}
                            </td>
                          </>
                        )}

                        <td className="px-3 py-2">
                          <Tooltip content={p.playerId > 0 ? "Double-click to open" : undefined}>
                            <span>{p.rulerLabel}</span>
                          </Tooltip>
                        </td>
                      </tr>
                    );
                  });
                })
              )}
            </tbody>
          </table>
        </div>
      )}
    </Modal>
  );
}
