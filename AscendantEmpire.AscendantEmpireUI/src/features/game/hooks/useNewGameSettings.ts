import { useQuery } from "@tanstack/react-query";
import { getNewGameSettings, type NewGameSettings } from "@/features/game/api/gameSettingsApi";

export const newGameSettingsKey = ["newGameSettings"] as const;

/**
 * The settings a new game must be created within.
 *
 * <remarks>
 * Server configuration, so it is the same for every account and changes only on a deploy: fetched once, kept indefinitely, and not refetched on
 * focus. The dialog passes its own open state as `enabled` so nothing is requested until a user actually opens it, after which reopening is free.
 * </remarks>
 */
export function useNewGameSettingsQuery(enabled: boolean) {
  return useQuery<NewGameSettings, Error>({
    queryKey: newGameSettingsKey,
    enabled,
    refetchOnWindowFocus: false,
    staleTime: Infinity,
    queryFn: () => getNewGameSettings(),
  });
}
