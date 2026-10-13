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
  // True when the player holding the slot has been through setup, so the names and colour above are theirs rather than
  // seeded placeholders. Always false on an unclaimed slot, and the other half of what a game waits on before starting.
  isDefined: boolean;
};
