import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Form, Modal, Spinner, Table } from "react-bootstrap";
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
  const [selected, setSelected] = useState<{ gameId: number; playerGuid: string } | null>(null);

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

    const p = g.players.find((x) => x.playerGuid === selected.playerGuid);
    if (!p) return null;

    return {
      gameId: g.gameId,
      gameGuid: g.gameGuid,
      gameName: g.gameName,
      width: g.mapWidth,
      height: g.mapHeight,
      playerGuid: p.playerGuid,
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
    <Modal show={show} onHide={onClose} centered size="lg">
      <Modal.Header closeButton>
        <Modal.Title>Open Game</Modal.Title>
      </Modal.Header>

      <Modal.Body>
        <div className="d-flex align-items-center gap-2 mb-3">
          <Form.Control placeholder="Filter by game or player..." value={filter} onChange={(e) => setFilter(e.target.value)} />
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
          <div className="d-flex align-items-center gap-2">
            <Spinner animation="border" size="sm" />
            <span>Loading games...</span>
          </div>
        ) : (
          <div style={{ maxHeight: 420, overflow: "auto" }}>
            <Table hover responsive className="mb-0">
              <thead>
                <tr>
                  <th style={{ width: "35%" }}>Game</th>
                  <th style={{ width: 120 }}>Size</th>
                  <th>Player</th>
                </tr>
              </thead>

              <tbody>
                {filteredGames.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="text-muted">
                      No games found.
                    </td>
                  </tr>
                ) : (
                  filteredGames.flatMap((g) => {
                    const players = g.players.length ? g.players : [{ playerGuid: "", rulerLabel: "(no players)", campaignOffsetId: 0 }];

                    return players.map((p, idx) => {
                      const rowKey = `${g.gameId}-${p.playerGuid || "none"}-${idx}`;
                      const isSelected = !!p.playerGuid && selected?.gameId === g.gameId && selected?.playerGuid === p.playerGuid;

                      const onClick = () => {
                        if (!p.playerGuid) return;
                        setSelected({ gameId: g.gameId, playerGuid: p.playerGuid });
                      };

                      const onDoubleClick = () => {
                        if (!p.playerGuid) return;

                        setSelected({ gameId: g.gameId, playerGuid: p.playerGuid });
                        // open immediately
                        const selection: OpenGameSelection = {
                          gameId: g.gameId,
                          gameGuid: g.gameGuid,
                          gameName: g.gameName,
                          width: g.mapWidth,
                          height: g.mapHeight,
                          playerGuid: p.playerGuid,
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
                          className={isSelected ? "table-active" : undefined}
                          role={p.playerGuid ? "button" : undefined}
                          onClick={onClick}
                          onDoubleClick={onDoubleClick}
                          title={p.playerGuid ? "Double-click to open" : undefined}>
                          {idx === 0 && (
                            <>
                              <td rowSpan={players.length}>
                                <div className="fw-semibold">{g.gameName}</div>
                                <div className="text-muted" style={{ fontSize: "0.85rem" }}>
                                  Players: {g.numberOfPlayers} • {g.gameGuid}
                                </div>
                              </td>

                              <td rowSpan={players.length}>
                                {g.mapWidth ?? "?"}×{g.mapHeight ?? "?"}
                              </td>
                            </>
                          )}

                          <td>{p.rulerLabel}</td>
                        </tr>
                      );
                    });
                  })
                )}
              </tbody>
            </Table>
          </div>
        )}
      </Modal.Body>

      <Modal.Footer>
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>

        <Button variant="primary" onClick={handleOpen} disabled={!selectedSelection}>
          Open
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
