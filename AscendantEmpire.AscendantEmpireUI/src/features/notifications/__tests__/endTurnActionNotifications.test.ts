import type { PossibleAction } from "@/features/game/api/turnApi";
import unitIconSrc from "@/assets/units/centurion_40px.png";
import { buildEndTurnActionGroups } from "../endTurnActionNotifications";

function forceAction(overrides: Partial<PossibleAction> = {}): PossibleAction {
  return {
    category: "Military",
    description: "Settler (0.5/1)",
    xCoord: 3,
    yCoord: 4,
    id: 11,
    recordTypeId: 1,
    unitTemplateId: 5,
    ...overrides,
  };
}

describe("buildEndTurnActionGroups", () => {
  it("groups a Force with movement left under the Forces heading, labelled as the server worded it", () => {
    const [group] = buildEndTurnActionGroups([forceAction()]);

    expect(group.label).toBe("Forces need orders");
    expect(group.items[0].label).toBe("Settler (0.5/1)");
  });

  it("draws the Force with its own template's image when the lookup has it", () => {
    const [group] = buildEndTurnActionGroups([forceAction()], new Map([[5, "/units/settler.png"]]));

    expect(group.items[0].iconSrc).toBe("/units/settler.png");
    expect(group.items[0].iconAlt).toBe("Unit");
  });

  it("falls back to the generic soldier while the template's image is unknown", () => {
    const [group] = buildEndTurnActionGroups([forceAction()], new Map());

    expect(group.items[0].iconSrc).toBe(unitIconSrc);
  });

  it("selects the Force on click, keeping its tile to pan to", () => {
    const [group] = buildEndTurnActionGroups([forceAction()]);

    expect(group.items[0].action).toEqual({ kind: "select-force", forceId: 11 });
    expect(group.items[0].location).toEqual({ x: 3, y: 4 });
  });
});
