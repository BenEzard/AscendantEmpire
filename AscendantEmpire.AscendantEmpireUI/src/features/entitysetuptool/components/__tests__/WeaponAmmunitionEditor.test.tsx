// Wiring tests for WeaponAmmunitionEditor — the kinds of ammunition a weapon accepts.
//
// Purely presentational: the Asset catalogue and the chosen options come in as props and the edited options go out through onChange, so there is no network
// here. What is pinned is the cap on how many a weapon may name, that only ammunition is offered, and that an Asset cannot be named twice.

import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import WeaponAmmunitionEditor from "../sections/properties/WeaponAmmunitionEditor";
import type { AssetOption } from "../../api/assetApi";
import { MAX_AMMUNITION_OPTIONS, type WeaponAmmunitionOption } from "@/features/unitdesign/types/UnitComponent";

const SLING_SHOT: AssetOption = { id: 1, code: "Weapons.Ammunition.SlingShot", displayName: "Sling Shot", iconPath: "/assets/sling-shot.png" };
const ARROWS: AssetOption = { id: 2, code: "Weapons.Ammunition.Arrows", displayName: "Arrows", iconPath: "/assets/arrows.png" };
// No icon of its own, so its row falls back to the generic glyph rather than rendering a broken image.
const BULLETS: AssetOption = { id: 3, code: "Weapons.Ammunition.Bullets", displayName: "Bullets", iconPath: "" };
const ARTILLERY: AssetOption = { id: 4, code: "Weapons.Ammunition.ArtilleryRounds", displayName: "Artillery Rounds", iconPath: "" };

const ALL_ASSETS = [SLING_SHOT, ARROWS, BULLETS, ARTILLERY];

function option(assetCode: string): WeaponAmmunitionOption {
  return { assetCode };
}

function renderEditor(options: WeaponAmmunitionOption[] = [], assets: AssetOption[] = ALL_ASSETS) {
  const onChange = vi.fn();

  render(<WeaponAmmunitionEditor options={options} assets={assets} onChange={onChange} />);

  return { onChange, user: userEvent.setup() };
}

it("adds the chosen Asset", async () => {
  const { onChange, user } = renderEditor();

  await user.selectOptions(screen.getByLabelText("Add ammunition"), ARROWS.code);

  expect(onChange).toHaveBeenCalledWith([option(ARROWS.code)]);
});

it("stops offering more once the weapon names the most it can", () => {
  renderEditor([option(SLING_SHOT.code), option(ARROWS.code), option(BULLETS.code)]);

  expect(MAX_AMMUNITION_OPTIONS).toBe(3);
  expect(screen.getByLabelText("Add ammunition")).toBeDisabled();
});

it("does not offer an Asset the weapon already names", () => {
  renderEditor([option(ARROWS.code)]);

  const picker = screen.getByLabelText("Add ammunition");

  expect(picker).not.toHaveTextContent(ARROWS.displayName);
  expect(picker).toHaveTextContent(BULLETS.displayName);
});

it("removes an option", async () => {
  const { onChange, user } = renderEditor([option(ARROWS.code), option(BULLETS.code)]);

  await user.click(screen.getByLabelText(`Remove ${ARROWS.displayName}`));

  expect(onChange).toHaveBeenCalledWith([option(BULLETS.code)]);
});

it("offers only ammunition Assets, so nothing else in the catalogue can be fired", () => {
  const oakLogs: AssetOption = { id: 9, code: "ConstructionMaterials.Wood.Logs.Oak", displayName: "Oak Log", iconPath: "" };

  renderEditor([], [ARROWS, oakLogs]);

  const picker = screen.getByLabelText("Add ammunition");

  expect(picker).toHaveTextContent(ARROWS.displayName);
  expect(picker).not.toHaveTextContent(oakLogs.displayName);
});

it("still names an option already on the weapon whose Asset is not ammunition, rather than hiding what is stored", () => {
  const oakLogs: AssetOption = { id: 9, code: "ConstructionMaterials.Wood.Logs.Oak", displayName: "Oak Log", iconPath: "" };

  renderEditor([option(oakLogs.code)], [ARROWS, oakLogs]);

  expect(screen.getByLabelText(`Remove ${oakLogs.displayName}`)).toBeInTheDocument();
});

it("tells two Assets sharing a display name apart by code, since an authored name is not guaranteed unique", async () => {
  const stoneShot: AssetOption = { id: 5, code: "Weapons.Ammunition.StoneShot", displayName: "Shot", iconPath: "" };
  const leadShot: AssetOption = { id: 6, code: "Weapons.Ammunition.LeadShot", displayName: "Shot", iconPath: "" };

  const { onChange, user } = renderEditor([], [stoneShot, leadShot, ARROWS]);

  const picker = screen.getByLabelText("Add ammunition");

  expect(picker).toHaveTextContent(`Shot (${stoneShot.code})`);
  expect(picker).toHaveTextContent(`Shot (${leadShot.code})`);
  // Arrows is the only Asset of its name, so it keeps the plain one.
  expect(picker).toHaveTextContent(ARROWS.displayName);

  await user.selectOptions(picker, leadShot.code);

  expect(onChange).toHaveBeenCalledWith([option(leadShot.code)]);
});

it("names an option whose Asset is not in the catalogue by its code, rather than dropping it", () => {
  renderEditor([option("Weapons.Ammunition.Renamed")]);

  expect(screen.getByText("Weapons.Ammunition.Renamed")).toBeInTheDocument();
});
