import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Modal, Spinner, Tooltip } from "@/components/ui";
import type { OpenGamesResponse } from "../types/OpenGamesResponse";
import type { OpenGame } from "../types/OpenGame";
import { getOpenGames } from "../hooks/getOpenGames";

export interface OpenGameDialogProps {
  show: boolean;
  onClose: () => void;
  /**
   * Hands back the game the user picked — the game only, never a player.
   *
   * Which player the user controls in it is the token's answer, not this dialog's: the caller enters
   * the game and takes the player from what the server issues. This listing is a public lobby (it
   * shows every active ruler in every in-progress game, whoever is asking), so a player chosen from
   * it would be a player the user picked rather than one they hold.
   */
  onOpenGame: (game: OpenGame) => void;
}

/**
 * Picks a game to open.
 *
 * The rulers are listed under each game because they are what tells one game from another — "the one
 * I'm in against Jen" — but they are read-only detail. Clicking any row of a game selects that game.
 */
export default function OpenGameDialog({ show, onClose, onOpenGame }: OpenGameDialogProps) {
  const [filter, setFilter] = useState("");
  const [selectedGameId, setSelectedGameId] = useState<number | null>(null);

  const { data, isLoading, isFetching, error, refetch } = useQuery<OpenGamesResponse, Error>({
    queryKey: ["games", "open"],
    queryFn: getOpenGames,
    enabled: show,
    refetchOnWindowFocus: false,
  });

  const games = useMemo<OpenGame[]>(() => data?.games ?? [], [data]);

  useEffect(() => {
    if (!show) {
      return;
    }

    setFilter("");
    setSelectedGameId(null);
  }, [show]);

  const filteredGames = useMemo(() => {
    const f = filter.trim().toLowerCase();
    if (!f) {
      return games;
    }

    return games.filter((g) => {
      if (g.gameName.toLowerCase().includes(f)) {
        return true;
      }
      return g.players.some((p) => p.rulerLabel.toLowerCase().includes(f));
    });
  }, [games, filter]);

  const selectedGame = useMemo<OpenGame | null>(() => {
    if (selectedGameId == null) {
      return null;
    }

    return games.find((g) => g.gameId === selectedGameId) ?? null;
  }, [selectedGameId, games]);

  const handleOpen = () => {
    if (!selectedGame) {
      return;
    }

    onOpenGame(selectedGame);
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
          <Button variant="primary" onClick={handleOpen} disabled={!selectedGame}>
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
                <th className="px-3 py-2">Rulers</th>
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
                  // A game with no active players is still listed, so it can be seen to exist, but it can't be opened:
                  // nobody holds a slot in it, so nobody can enter it.
                  const players = g.players.length ? g.players : [{ playerId: 0, rulerLabel: "(no players)", campaignOffsetId: 0 }];
                  const isOpenable = g.players.length > 0;
                  const isSelected = isOpenable && selectedGameId === g.gameId;

                  return players.map((p, idx) => {
                    const rowKey = `${g.gameId}-${p.playerId || "none"}-${idx}`;

                    // Every row of a game selects the game itself. The ruler on the row is detail about the game, not
                    // the identity being chosen — that comes back from the server when the game is entered.
                    const onClick = () => {
                      if (!isOpenable) {
                        return;
                      }
                      setSelectedGameId(g.gameId);
                    };

                    const onDoubleClick = () => {
                      if (!isOpenable) {
                        return;
                      }

                      setSelectedGameId(g.gameId);
                      onOpenGame(g);
                      onClose();
                    };

                    return (
                      <tr
                        key={rowKey}
                        className={[
                          "border-t border-slate-100",
                          isOpenable ? "cursor-pointer hover:bg-slate-50" : "",
                          isSelected ? "bg-slate-100" : "",
                        ].join(" ")}
                        role={isOpenable ? "button" : undefined}
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
                          <Tooltip content={isOpenable ? "Double-click to open this game" : undefined}>
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
