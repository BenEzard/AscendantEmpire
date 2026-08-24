// Battle-map movement points: how many tiles a design can cross in one battle turn.
//
// The formula is MAX(0, body power - the weight of everything fitted into the body). A heavier loadout buys
// fewer tiles, which is the design lever the whole unit-design system leans on.
//
// The body's OWN weight is deliberately excluded: power is what the body can move with once it is already
// carrying itself, so subtracting the body's weight as well would charge for it twice.
//
// This is a deliberate mirror of the server's BattleMovementCalculator
// (AscendantEmpire.Application/Features/UnitComponents/Services). The server is authoritative — it derives
// this value into the compiled template snapshot at compile time — but the designer has to show the number
// live as components are dragged in and out, long before anything is saved, so the arithmetic exists on both
// sides. The two are pinned to the same calibration cases in their respective tests
// (BattleMovementCalculatorTests.cs and unitDesign.test.ts); if you change one, change the other.

/** The lowest movement a design can derive to: an over-loaded design stands still rather than going negative. */
export const MINIMUM_BATTLE_MOVEMENT_POINTS_RAW = 0;

/**
 * Calculates battle-map movement points for a body of the given power carrying the given weight of fitted
 * components. Both arguments and the result are raw Scale-1,000 fixed-point values (1,000 raw = 1.0), so a
 * half-tile result such as 6.5 comes back as 6,500.
 *
 * `carriedWeightRaw` must exclude the body's own weight — see the note at the top of this file.
 */
export function calculateBattleMovementPoints(bodyPowerRaw: number, carriedWeightRaw: number): number {
  return Math.max(MINIMUM_BATTLE_MOVEMENT_POINTS_RAW, bodyPowerRaw - carriedWeightRaw);
}
