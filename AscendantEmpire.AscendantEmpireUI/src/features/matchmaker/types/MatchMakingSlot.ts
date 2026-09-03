// Mirrors the API's IMatchMakingSlot (camelCased by ASP.NET's JSON serializer).
export type MatchMakingSlot = {
  playerId: number;
  rulerTitle: string;
  rulerName: string;
  factionName: string;
  // The colour this slot's player picked, or null while nobody has picked one for it.
  factionColour: string | null;
  rulerLabel: string;
  // True when some account already controls this slot (it can't be claimed).
  isClaimed: boolean;
  // True when *this* logged-in user is the one controlling the slot.
  isClaimedByCurrentUser: boolean;
};
