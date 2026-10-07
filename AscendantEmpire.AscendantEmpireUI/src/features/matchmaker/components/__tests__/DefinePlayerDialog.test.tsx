// Wiring tests for DefinePlayerDialog: what the commit gate lets through, where a failed save lands the
// user, that the title suggestions follow the chosen gender, and that a lost colour race is reported
// where the fix is.
//
// The API module is mocked rather than the http client, so these exercise the dialog's own behaviour
// without asserting anything about URL shapes — matchMakingApi is where those live.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { ApiError } from "@/api/http";
import DefinePlayerDialog from "../DefinePlayerDialog";
// vi.mock below is hoisted above this import, so these bindings are already the mocked functions.
import { getPlayerDefinition, updatePlayerDefinition } from "../../api/matchMakingApi";
import type { PlayerDefinition } from "../../types/PlayerDefinition";

vi.mock("../../api/matchMakingApi", () => ({
  getPlayerDefinition: vi.fn(),
  updatePlayerDefinition: vi.fn(),
}));

// Nothing in the vitest config clears mocks between tests, and several of these assert on call counts.
beforeEach(() => {
  vi.clearAllMocks();
});

const GAME_ID = 7;

/** A rival's colour, so the picker has something to strike through. */
const TAKEN_COLOUR = "#c10007";

/** An undefined slot as the API returns one: placeholder title, no gender, no colour. */
function undefinedSlot(overrides: Partial<PlayerDefinition> = {}): PlayerDefinition {
  return {
    playerId: 72,
    gameId: GAME_ID,
    rulerTitle: "Nobody",
    rulerName: "Player 3",
    rulerGender: null,
    factionName: "Faction 3",
    factionColour: null,
    // Seeded placeholders, like the ruler and faction names above: the API never sends these back blank for a
    // slot a game created, so the dialog opens with all four already answered.
    peopleName: "People 3",
    demonym: "Person 3",
    cultureName: "Culture 3",
    language: "Language 3",
    // Empty, which is how a game seeds every slot: the crown stands in until the player picks a face.
    rulerPortraitPath: "",
    usedFactionColours: [{ value: TAKEN_COLOUR, usedBy: "Faction 1" }],
    usedRulerPortraits: [],
    ...overrides,
  };
}

function renderDialog(definition: PlayerDefinition = undefinedSlot()) {
  vi.mocked(getPlayerDefinition).mockResolvedValue(definition);

  const onCancel = vi.fn();
  const onSaved = vi.fn();

  // Retries off so a rejected save surfaces immediately instead of being retried past the assertion.
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <DefinePlayerDialog open gameId={GAME_ID} onCancel={onCancel} onSaved={onSaved} />
    </QueryClientProvider>,
  );

  return { onCancel, onSaved };
}

const saveButton = () => screen.getByRole("button", { name: "Save" });
const rulerNameInput = () => screen.getByRole("textbox", { name: "Ruler Name" });
const rulerTitleInput = () => screen.getByRole("combobox", { name: "Ruler Title" });
const factionNameInput = () => screen.getByRole("textbox", { name: "Faction Name" });
const peopleNameInput = () => screen.getByRole("textbox", { name: "People Name" });
const demonymInput = () => screen.getByRole("textbox", { name: "Demonym" });
const cultureNameInput = () => screen.getByRole("textbox", { name: "Culture Name" });
const languageInput = () => screen.getByRole("textbox", { name: "Language" });

// Nav buttons render as "<number> <label>" (plus "has validation errors" when the page is marked
// invalid), so anchor on the number. A loose /Faction/ would also match the colour picker's trigger,
// which is named "Faction colour" and is in the DOM the whole time — PagedDialog renders every page.
const rulerTab = () => screen.getByRole("button", { name: /^\d+\s*Ruler\b/ });
const factionTab = () => screen.getByRole("button", { name: /^\d+\s*Faction\b/ });

/** The colour picker's trigger square, named by the `title` passed to it. */
const colourTrigger = () => screen.getByRole("button", { name: "Faction colour" });

/** The portrait picker's trigger thumbnail, named by the `title` passed to it. */
const portraitTrigger = () => screen.getByRole("button", { name: "Ruler portrait" });

/** One portrait tile in the opened grid. ImagePicker labels each with the item's label, e.g. "Portrait 9". */
const portraitTile = (label: string) => screen.getByRole("button", { name: label });

/** One palette swatch. ColorPicker labels each with its Tailwind description, e.g. "purple-700". */
const swatch = (description: string) => screen.getByRole("button", { name: description });

/** The nav button of the page currently on screen. */
function activeTabName(): string {
  const current = screen.getAllByRole("button").find((button) => button.getAttribute("aria-current") === "page");
  return current?.textContent?.trim() ?? "";
}

/** Waits for the fetched definition to have seeded the form. */
async function waitForLoad() {
  await waitFor(() => expect(rulerNameInput()).toHaveValue("Player 3"));
}

/** Fills in everything the commit gate asks for, leaving the title blank. */
async function completeTheForm(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("radio", { name: "Female" }));

  await user.click(factionTab());
  await user.click(colourTrigger());
  await user.click(swatch("purple-700"));
}

describe("the commit gate", () => {
  it("saves once the required fields are filled in, sending a blank title as blank", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1]).toEqual({
      // Blank, not "Nobody": the server owns the placeholder, so the client never sends it.
      rulerTitle: "",
      rulerName: "Player 3",
      rulerGender: "Female",
      factionName: "Faction 3",
      factionColour: "#8200db",
      peopleName: "People 3",
      demonym: "Person 3",
      cultureName: "Culture 3",
      language: "Language 3",
      rulerPortraitPath: "",
    });
  });

  it("keeps its errors hidden until the first save attempt", async () => {
    renderDialog();
    await waitForLoad();

    expect(screen.queryByText("Choose a gender for your ruler.")).not.toBeInTheDocument();
  });

  it("does not save while the gender is unpicked", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.click(saveButton());

    expect(vi.mocked(updatePlayerDefinition)).not.toHaveBeenCalled();
    expect(screen.getByText("Choose a gender for your ruler.")).toBeInTheDocument();
  });

  it("moves the user to the page holding the problem when the failure is out of sight", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    // Everything on the Ruler page is answered; only the colour, on the other page, is missing.
    await user.click(screen.getByRole("radio", { name: "Male" }));
    await user.click(saveButton());

    expect(activeTabName()).toContain("Faction");
    expect(screen.getByText("Pick a colour for your faction.")).toBeInTheDocument();
  });

  it("does not save with a blank ruler name", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.clear(rulerNameInput());
    await completeTheForm(user);
    await user.click(saveButton());

    expect(vi.mocked(updatePlayerDefinition)).not.toHaveBeenCalled();
    expect(activeTabName()).toContain("Ruler");
  });
});

describe("the people and culture fields", () => {
  it("opens on what the slot already holds", async () => {
    renderDialog();
    await waitForLoad();

    expect(peopleNameInput()).toHaveValue("People 3");
    expect(demonymInput()).toHaveValue("Person 3");
    expect(cultureNameInput()).toHaveValue("Culture 3");
    expect(languageInput()).toHaveValue("Language 3");
  });

  // A slot from before these columns existed comes back with all four blank, which is the only way the dialog
  // ever opens on an unanswered one — hence clearing the field by hand to reach the same state.
  it.each([
    ["People Name", peopleNameInput, "Give your people a name."],
    ["Demonym", demonymInput, "Say what one of your people is called."],
    ["Culture Name", cultureNameInput, "Give your people's culture a name."],
    ["Language", languageInput, "Name the language your people speak."],
  ])("refuses to save with a blank %s", async (_label, input, message) => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.clear(input());
    await user.click(saveButton());

    expect(vi.mocked(updatePlayerDefinition)).not.toHaveBeenCalled();
    expect(screen.getByText(message)).toBeInTheDocument();
  });

  it("sends them trimmed", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.clear(peopleNameInput());
    await user.type(peopleNameInput(), "  The Vashti  ");
    await user.click(saveButton());

    await waitFor(() => expect(vi.mocked(updatePlayerDefinition)).toHaveBeenCalled());
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].peopleName).toBe("The Vashti");
  });
});

describe("the ruler title", () => {
  it("shows the seeded placeholder as an empty field rather than something to delete", async () => {
    renderDialog();
    await waitForLoad();

    expect(rulerTitleInput()).toHaveValue("");
  });

  it("suggests only the non-gendered titles until a gender is picked", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.click(rulerTitleInput());

    expect(screen.getByRole("option", { name: "Sovereign" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "King" })).not.toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Queen" })).not.toBeInTheDocument();
  });

  it("switches its suggestions to match the chosen gender", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.click(screen.getByRole("radio", { name: "Female" }));
    await user.click(rulerTitleInput());

    expect(screen.getByRole("option", { name: "Queen" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "King" })).not.toBeInTheDocument();
    // The non-gendered titles stay on offer alongside them.
    expect(screen.getByRole("option", { name: "Sovereign" })).toBeInTheDocument();
  });

  it("keeps free text that matches no suggestion", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    renderDialog();
    await waitForLoad();

    await user.type(rulerTitleInput(), "Grand Nagus");
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(vi.mocked(updatePlayerDefinition)).toHaveBeenCalled());
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerTitle).toBe("Grand Nagus");
  });
});

describe("faction colours", () => {
  it("marks a colour another player holds as unavailable", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.click(factionTab());
    await user.click(colourTrigger());

    // red-700 is the rival's colour; the picker disables that swatch and leaves the rest pickable.
    expect(swatch("red-700")).toBeDisabled();
    expect(swatch("purple-700")).toBeEnabled();
  });

  it("reports a lost colour race on the colour field, on the page that owns it", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(
      new ApiError("That colour has already been taken by another player in this game.", 409, undefined),
    );
    const { onSaved } = renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    // Move away, so landing back on Faction is the dialog's doing rather than where we already were.
    await user.click(rulerTab());
    await user.click(saveButton());

    await waitFor(() =>
      expect(screen.getByText("That colour has already been taken by another player in this game.")).toBeInTheDocument(),
    );
    expect(activeTabName()).toContain("Faction");
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("the ruler portrait", () => {
  it("opens on the crown when the slot has not chosen one, which is how a game seeds every slot", async () => {
    renderDialog();
    await waitForLoad();

    expect(portraitTrigger().querySelector("img")).toBeNull();
  });

  it("opens on the portrait the slot already holds", async () => {
    renderDialog(undefinedSlot({ rulerPortraitPath: "/portraits/ruler_03.png" }));
    await waitForLoad();

    // The trigger carries the chosen portrait as its image, which is the only visible statement of what is chosen.
    expect(portraitTrigger().querySelector("img")).toHaveAttribute("src", "/portraits/ruler_03.png");
  });

  it("sends the portrait the player picked from the grid", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog();
    await waitForLoad();

    await user.click(portraitTrigger());
    await user.click(portraitTile("Portrait 9"));
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerPortraitPath).toBe("/portraits/ruler_09.png");
  });

  // The crown is the one portrait value a game may hold more than once, so leaving it is a real answer rather than an
  // unfinished field — unlike the gender and the colour, which the commit gate does insist on.
  it("saves with no portrait at all, sending the crown as an empty path", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerPortraitPath).toBe("");
  });

  // A portrait file since removed or renamed leaves a path the picker cannot offer. Reading it as the crown is what
  // stops the field looking unset while holding a value it would save straight back.
  it("falls back to the crown for a path the picker no longer offers", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog(undefinedSlot({ rulerPortraitPath: "/portraits/ruler_99.png" }));
    await waitForLoad();

    expect(portraitTrigger().querySelector("img")).toBeNull();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerPortraitPath).toBe("");
  });
});

describe("portraits a rival already holds", () => {
  const TAKEN_PORTRAIT = "/portraits/ruler_04.png";

  function aSlotWithARivalPortrait(overrides: Partial<PlayerDefinition> = {}): PlayerDefinition {
    return undefinedSlot({ usedRulerPortraits: [{ value: TAKEN_PORTRAIT, usedBy: "Faction 1" }], ...overrides });
  }

  it("cannot be picked, and say who holds them", async () => {
    const user = userEvent.setup();
    renderDialog(aSlotWithARivalPortrait());
    await waitForLoad();

    await user.click(portraitTrigger());

    expect(portraitTile("Portrait 4")).toBeDisabled();
    expect(portraitTile("Portrait 1")).toBeEnabled();
  });

  it("stay unpicked when clicked, so a save cannot carry one", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog(aSlotWithARivalPortrait());
    await waitForLoad();

    await user.click(portraitTrigger());
    await user.click(portraitTile("Portrait 4"));
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerPortraitPath).toBe("");
  });

  // The used list is every portrait held in the game, the caller's own included, so the picker has to exempt the
  // current selection or re-editing a defined slot would offer no way back to the portrait it already has.
  it("do not include the player's own, which stays pickable", async () => {
    const user = userEvent.setup();
    renderDialog(
      aSlotWithARivalPortrait({
        rulerPortraitPath: TAKEN_PORTRAIT,
        usedRulerPortraits: [{ value: TAKEN_PORTRAIT, usedBy: "Faction 1" }],
      }),
    );
    await waitForLoad();

    await user.click(portraitTrigger());

    expect(portraitTile("Portrait 4")).toBeEnabled();
    expect(portraitTile("Portrait 4")).toHaveAttribute("aria-pressed", "true");
  });

  it("report a lost race against the portrait field, on the Ruler page", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(
      new ApiError("That portrait has already been taken by another player in this game.", 409, undefined),
    );
    const { onSaved } = renderDialog();
    await waitForLoad();

    await user.click(portraitTrigger());
    await user.click(portraitTile("Portrait 9"));
    // Finish on the other page, so landing back on Ruler is the dialog's doing rather than where we already were.
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() =>
      expect(screen.getByText("That portrait has already been taken by another player in this game.")).toBeInTheDocument(),
    );
    expect(activeTabName()).toContain("Ruler");
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("an already-defined slot", () => {
  it("opens on what the player chose last time", async () => {
    renderDialog(
      undefinedSlot({
        rulerTitle: "Empress",
        rulerName: "Aelara",
        rulerGender: "Female",
        factionName: "The Ember Pact",
        factionColour: "#8200db",
        peopleName: "The Vashti",
        demonym: "Vashti",
        cultureName: "Vashti Highland",
        language: "Old Vashti",
        rulerPortraitPath: "/portraits/ruler_14.png",
      }),
    );

    await waitFor(() => expect(rulerNameInput()).toHaveValue("Aelara"));

    expect(rulerTitleInput()).toHaveValue("Empress");
    expect(screen.getByRole("radio", { name: "Female" })).toBeChecked();
    expect(factionNameInput()).toHaveValue("The Ember Pact");
    expect(peopleNameInput()).toHaveValue("The Vashti");
    expect(demonymInput()).toHaveValue("Vashti");
    expect(cultureNameInput()).toHaveValue("Vashti Highland");
    expect(languageInput()).toHaveValue("Old Vashti");
    expect(portraitTrigger().querySelector("img")).toHaveAttribute("src", "/portraits/ruler_14.png");
  });
});
