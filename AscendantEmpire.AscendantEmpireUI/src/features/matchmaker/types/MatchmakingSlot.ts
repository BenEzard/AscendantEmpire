// Mirrors the API's IMatchmakingSlot (camelCased by ASP.NET's JSON serializer).
export type MatchmakingSlot = {
  playerId: number;
  rulerTitle: string;
  rulerName: string;
  factionName: string;
  rulerLabel: string;
  // True when some account already controls this slot (it can't be claimed).
  isClaimed: boolean;
  // True when *this* logged-in user is the one controlling the slot.
  isClaimedByCurrentUser: boolean;
};
