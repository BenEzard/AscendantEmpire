import { http } from "@/api/http";

/** The API's `PlayerStatus` member names. */
export type GamePlayerStatus = "PendingAddition" | "Active" | "ExcludedDueToInactivity" | "Eliminated" | "Winner" | "Banned";

/**
 * One player in the entered game, as any other player in it may see them.
 *
 * Public identity only. Used to name whose force an intelligence report is about - which the report itself carries only as an id - and to show the
 * ruler whose turn is currently open in the game toolbar.
 */
export type GamePlayer = {
  playerId: number;
  /** "Title Name (Faction)". */
  rulerLabel: string;
  factionName: string;
  /** The faction's colour, or null while the player has not picked one. */
  factionColour: string | null;
  /** Root-relative path to the ruler's portrait. Empty on a slot predating the column. */
  rulerPortraitPath: string;
  status: GamePlayerStatus;
};

type GamePlayersWireDto = Partial<{ players: Partial<GamePlayer>[] }>;

/** Every player in the entered game, whatever their status. The API reads the game from the game token. */
export async function getGamePlayers(): Promise<GamePlayer[]> {
  const { data } = await http.get<GamePlayersWireDto>("/player/game-players");

  return (data.players ?? [])
    .filter((player) => player.playerId != null)
    .map((player) => ({
      playerId: player.playerId!,
      rulerLabel: player.rulerLabel ?? "",
      factionName: player.factionName ?? "",
      factionColour: player.factionColour ?? null,
      rulerPortraitPath: player.rulerPortraitPath ?? "",
      status: player.status ?? "Active",
    }));
}
