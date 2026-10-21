// Wiring tests for NewGameDialog: that Create is always pressable and reports what is wrong, that a problem on the
// other page sends the user there, that the battle map size is a stepped slider built from the server's settings, and
// that the two field descriptions are tooltips rather than text beside the control.
//
// The settings api module is mocked rather than the http client, so these exercise the dialog's own behaviour without
// asserting anything about URL shapes — gameSettingsApi is where those live. The map picker is stubbed for the same
// reason: choosing a map is its business, and the dialog only cares whether one has been chosen.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { http } from "@/api/http";
import NewGameDialog from "../NewGameDialog";
// vi.mock below is hoisted above this import, so this binding is already the mocked function.
import { getNewGameSettings, type NewGameSettings } from "@/features/game/api/gameSettingsApi";

vi.mock("@/features/game/api/gameSettingsApi", () => ({
  getNewGameSettings: vi.fn(),
}));

vi.mock("@/api/http", () => ({
  http: { post: vi.fn() },
}));

// Stands in for the map gallery page: a single button that picks a map, which is all the dialog's commit gate reads.
const PICKED_MAP_ID = 42;

vi.mock("@/features/mapgallery/components/SelectCampaignMap", () => ({
  default: ({ onSelectedMapIdChange }: { onSelectedMapIdChange: (value: number | null) => void }) => (
    <button type="button" onClick={() => onSelectedMapIdChange(PICKED_MAP_ID)}>
      Pick a map
    </button>
  ),
}));

// Nothing in the vitest config clears mocks between tests, and several of these assert on call counts.
beforeEach(() => {
  vi.clearAllMocks();
});

/**
 * The settings as the server sends them, with the battle map bounds and step the slider is built from.
 *
 * The three battle map numbers are all multiples of the increment, which is what the shipped configuration guarantees —
 * a fixture that broke that would be testing a server misconfiguration rather than the dialog.
 */
function newGameSettings(overrides: Partial<NewGameSettings["battleMapSettings"]> = {}): NewGameSettings {
  return {
    minimumPlayersPerGame: 2,
    maximumPlayersPerGame: 18,
    battleMapSettings: {
      minimumBattleMapSize: 18,
      maximumBattleMapSize: 51,
      defaultBattleMapSize: 51,
      battleMapSizeIncrement: 3,
      ...overrides,
    },
    technologySettings: {
      baseResearchPoints: 100,
      technologySpeedMultiplierPercent: 1000,
      technologyFuzzinessPercent: 0,
    },
  };
}

function renderDialog(settings: NewGameSettings = newGameSettings()) {
  vi.mocked(getNewGameSettings).mockResolvedValue(settings);
  vi.mocked(http.post).mockResolvedValue({ data: 1 } as never);

  const onClose = vi.fn();
  const onCreateSuccess = vi.fn();

  // Retries off so a rejected create surfaces immediately instead of being retried past the assertion.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <NewGameDialog show onClose={onClose} onCreateSuccess={onCreateSuccess} />
    </QueryClientProvider>,
  );

  return { onClose, onCreateSuccess };
}

const createButton = () => screen.getByRole("button", { name: "Create" });
const gameNameInput = () => screen.getByRole("textbox", { name: "Game Name" });
const seedInput = () => screen.getByRole("textbox", { name: "Seed" });
const battleMapSizeSlider = () => screen.getByRole("slider", { name: "Battle Map Size" }) as HTMLInputElement;

// The nav buttons, whose accessible name is the page number followed by the label.
const generalTab = () => screen.getByRole("button", { name: /^\d+\s*General\b/ });
const mapTab = () => screen.getByRole("button", { name: /^\d+\s*Map & Features\b/ });

/** The nav button of the page currently on screen. */
function activeTabName(): string {
  const current = screen.getAllByRole("button").find((button) => button.getAttribute("aria-current") === "page");
  return current?.textContent?.trim() ?? "";
}

/**
 * Waits for the fetched settings to have landed.
 *
 * Anchored on the Players slider existing: both sliders are replaced by a "Loading..." note until the settings arrive,
 * so this is the one thing that tells the two states apart.
 */
async function settingsLoaded() {
  await screen.findByRole("slider", { name: "Players" });
}

/** Fills in everything a successful create needs, leaving the seed blank for a random one. */
async function completeTheForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(gameNameInput(), "Ascendant");
  await user.click(mapTab());
  await user.click(screen.getByRole("button", { name: "Pick a map" }));
  await user.click(generalTab());
}

describe("the commit gate", () => {
  it("leaves Create pressable on a freshly opened dialog, with nothing filled in and no errors shown", async () => {
    renderDialog();
    await settingsLoaded();

    expect(createButton()).toBeEnabled();
    // The point of an always-pressable button is that pressing it is what reveals the problems, so none may be on
    // screen before it has been pressed.
    expect(screen.queryByText("Give your game a name.")).toBeNull();
    expect(screen.queryByText("Choose the campaign map this game is played on.")).toBeNull();
  });

  it("reports a blank game name on the first press and sends nothing", async () => {
    const user = userEvent.setup();
    renderDialog();
    await settingsLoaded();

    await user.click(createButton());

    expect(await screen.findByText("Give your game a name.")).toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();
  });

  it("sends the user to the Map page when the only thing missing is the map", async () => {
    const user = userEvent.setup();
    renderDialog();
    await settingsLoaded();

    await user.type(gameNameInput(), "Ascendant");
    await user.click(createButton());

    // The missing map is on the page the user is not looking at, so Create has to move them to it or it looks as
    // though it did nothing at all.
    expect(activeTabName()).toContain("Map & Features");
    expect(await screen.findByText("Choose the campaign map this game is played on.")).toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();
  });

  it("refuses a seed that is not a number, and accepts a blank one as a request for a random seed", async () => {
    const user = userEvent.setup();
    const { onCreateSuccess } = renderDialog();
    await settingsLoaded();

    await completeTheForm(user);
    await user.type(seedInput(), "not-a-number");
    await user.click(createButton());

    expect(await screen.findByText("Seed must be a number, or leave blank for a random seed.")).toBeInTheDocument();
    expect(http.post).not.toHaveBeenCalled();

    await user.clear(seedInput());
    await user.click(createButton());

    await waitFor(() => expect(onCreateSuccess).toHaveBeenCalled());
    // Omitted rather than sent as null or 0: a blank seed asks the server to pick one.
    expect(http.post).toHaveBeenCalledWith("/games/setup", {
      mapId: PICKED_MAP_ID,
      gameName: "Ascendant",
      numberOfPlayers: 2,
      battleMapSize: 51,
      generationSeed: undefined,
    });
  });

  it("sends the chosen settings once everything passes", async () => {
    const user = userEvent.setup();
    const { onClose, onCreateSuccess } = renderDialog();
    await settingsLoaded();

    await completeTheForm(user);
    await user.type(seedInput(), "1234");
    await user.click(createButton());

    await waitFor(() => expect(onClose).toHaveBeenCalled());

    expect(http.post).toHaveBeenCalledWith("/games/setup", {
      mapId: PICKED_MAP_ID,
      gameName: "Ascendant",
      numberOfPlayers: 2,
      battleMapSize: 51,
      generationSeed: 1234,
    });
    expect(onCreateSuccess).toHaveBeenCalledWith('Game "Ascendant" created successfully.');
  });
});

describe("the battle map size control", () => {
  it("is a slider carrying the server's bounds and step, so no off-step size can be chosen", async () => {
    renderDialog();
    await settingsLoaded();

    const slider = battleMapSizeSlider();

    expect(slider).toHaveAttribute("min", "18");
    expect(slider).toHaveAttribute("max", "51");
    expect(slider).toHaveAttribute("step", "3");
    // Opens on the server's own starting value rather than on the bottom of the track.
    expect(slider.value).toBe("51");
  });

  it("follows the settings rather than any bound of its own", async () => {
    renderDialog(newGameSettings({ minimumBattleMapSize: 12, maximumBattleMapSize: 24, defaultBattleMapSize: 12, battleMapSizeIncrement: 6 }));
    await settingsLoaded();

    const slider = battleMapSizeSlider();

    expect(slider).toHaveAttribute("min", "12");
    expect(slider).toHaveAttribute("max", "24");
    expect(slider).toHaveAttribute("step", "6");
  });
});

describe("the field descriptions", () => {
  it("carries the battle map size and seed explanations as hover hints rather than text beside the control", async () => {
    renderDialog();
    await settingsLoaded();

    // HintIcon renders the hint as the icon's accessible name and shows it in a bubble on hover.
    expect(screen.getByRole("img", { name: "Width and height of Battle Map" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Leave blank for a random seed" })).toBeInTheDocument();

    // The wording that used to sit next to each control is gone from the page body.
    expect(screen.queryByText(/width & height of battle maps/i)).toBeNull();
    expect(screen.queryByText("Leave blank for a random seed")).toBeNull();
  });
});
