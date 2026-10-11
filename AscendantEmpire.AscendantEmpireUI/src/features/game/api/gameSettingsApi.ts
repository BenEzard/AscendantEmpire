import { http } from "@/api/http";

/** The bounds a new game's battle map size must sit within, and the size the New Game dialog opens on. */
export type BattleMapSettings = {
  minimumBattleMapSize: number;
  maximumBattleMapSize: number;
  defaultBattleMapSize: number;
};

/** The technology configuration a new game is created with. Reported for completeness; no screen offers these yet. */
export type TechnologySettings = {
  baseResearchPoints: number;
  technologySpeedMultiplierPercent: number;
  technologyFuzzinessPercent: number;
};

/** Everything the New Game dialog needs to build its controls: the server owns these, so the form never hardcodes a bound of its own. */
export type NewGameSettings = {
  minimumPlayersPerGame: number;
  maximumPlayersPerGame: number;
  battleMapSettings: BattleMapSettings;
  technologySettings: TechnologySettings;
};

type NewGameSettingsWireDto = Partial<{
  minimumPlayersPerGame: number;
  maximumPlayersPerGame: number;
  battleMapSettings: Partial<BattleMapSettings>;
  technologySettings: Partial<TechnologySettings>;
}>;

/**
 * Reads one required number off the payload, failing loudly when it is absent or not a number.
 *
 * Deliberately unlike the defaulting the other api modules do for display fields. Every value here is a *rule* the form enforces, so coalescing a
 * missing one to 0 would hand the dialog a bound that silently rejects everything, or offers a size the save then refuses. A thrown error instead
 * leaves the query failed, which is what keeps Create disabled.
 */
function requireNumber(value: number | undefined, name: string): number {
  if (typeof value !== "number" || Number.isFinite(value) === false) {
    throw new Error(`New game settings are incomplete: ${name} is missing.`);
  }

  return value;
}

/** The settings a new game must be created within. There is nothing to send: these are server configuration, the same for every account. */
export async function getNewGameSettings(): Promise<NewGameSettings> {
  const { data } = await http.get<NewGameSettingsWireDto>("/games/settings");

  const battleMapSettings = data.battleMapSettings ?? {};
  const technologySettings = data.technologySettings ?? {};

  return {
    minimumPlayersPerGame: requireNumber(data.minimumPlayersPerGame, "minimumPlayersPerGame"),
    maximumPlayersPerGame: requireNumber(data.maximumPlayersPerGame, "maximumPlayersPerGame"),
    battleMapSettings: {
      minimumBattleMapSize: requireNumber(battleMapSettings.minimumBattleMapSize, "minimumBattleMapSize"),
      maximumBattleMapSize: requireNumber(battleMapSettings.maximumBattleMapSize, "maximumBattleMapSize"),
      defaultBattleMapSize: requireNumber(battleMapSettings.defaultBattleMapSize, "defaultBattleMapSize"),
    },
    technologySettings: {
      baseResearchPoints: requireNumber(technologySettings.baseResearchPoints, "baseResearchPoints"),
      technologySpeedMultiplierPercent: requireNumber(technologySettings.technologySpeedMultiplierPercent, "technologySpeedMultiplierPercent"),
      technologyFuzzinessPercent: requireNumber(technologySettings.technologyFuzzinessPercent, "technologyFuzzinessPercent"),
    },
  };
}
