// Wiring tests for DefinePlayerDialog: what the commit gate lets through, where a failed save lands the
// user, that the title suggestions follow the chosen gender, and that a lost colour race is reported
// where the fix is.
//
// The API module is mocked rather than the http client, so these exercise the dialog's own behaviour
// without asserting anything about URL shapes — matchMakingApi is where those live.

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

/**
 * An undefined slot as the API returns one: placeholder title and names, no gender, no colour.
 *
 * Every name here is the game's own seeding. The dialog recognises that - no gender and no colour together mean no
 * save has ever been through it - and opens the identity fields blank, so these values are what it must *not* show.
 */
function undefinedSlot(overrides: Partial<PlayerDefinition> = {}): PlayerDefinition {
  return {
    playerId: 72,
    gameId: GAME_ID,
    rulerTitle: "Nobody",
    rulerName: "Player 3",
    rulerGender: null,
    factionName: "Faction 3",
    factionColour: null,
    // Seeded placeholders, like the ruler and faction names above. The API never sends these back blank for a slot
    // a game created; the dialog blanks them itself rather than making the player delete four stand-ins.
    peopleName: "People 3",
    demonym: "Person 3",
    cultureName: "Culture 3",
    language: "Language 3",
    // Empty, which is how a game seeds every slot: the crown stands in until the player picks a face.
    rulerPortraitPath: "",
    usedFactionColours: [{ value: TAKEN_COLOUR, usedBy: "Faction 1" }],
    usedRulerPortraits: [],
    // The lobby state, where every field is still editable. The locked case has its own block below.
    isLocked: false,
    ...overrides,
  };
}

/**
 * A slot its player has already defined: every name their own, plus the gender and colour a save insists on.
 *
 * That pair is what tells the dialog the names are answers rather than seeding, so a block testing what it opens on
 * has to set them - an override of the names alone would still be read as an undefined slot.
 */
function definedSlot(overrides: Partial<PlayerDefinition> = {}): PlayerDefinition {
  return undefinedSlot({
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
    ...overrides,
  });
}

/**
 * A 409 shaped the way the definition endpoint sends one: a message per clash, plus the detail naming the fields.
 *
 * The dialog places each message by the field name in the detail rather than by matching the message text, so a test
 * that omitted the detail would pass with the messages landing nowhere.
 */
function clashError(...fields: { field: string; message: string }[]): ApiError {
  return new ApiError(fields.map((f) => f.message).join(" "), 409, { fields });
}

const COLOUR_TAKEN = { field: "factionColour", message: "That colour has already been taken by another player in this game." };
const PORTRAIT_TAKEN = { field: "rulerPortraitPath", message: "That portrait has already been taken by another player in this game." };
const PEOPLE_TAKEN = { field: "peopleName", message: "That people name has already been taken by another player in this game." };
const DEMONYM_TAKEN = { field: "demonym", message: "That demonym has already been taken by another player in this game." };

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

/**
 * Waits for the fetched definition to have landed.
 *
 * Anchored on the Ruler page's first field existing rather than on a value: the page shows a spinner in place of its
 * fields while the fetch is in flight, and an undefined slot's fields are blank once it is not - so there is no value
 * to wait for that would tell the two apart.
 */
async function waitForLoad() {
  await waitFor(() => expect(rulerNameInput()).toBeInTheDocument());
}

/**
 * Puts a value into one of the plain text inputs in a single event.
 *
 * Deliberately not `user.type`, which these tests use everywhere a keystroke is the point. Six names at a dozen or
 * so characters each is ~90 simulated keystrokes per test, and in the full suite - where every file is competing for
 * the event loop - that is enough to carry a test past the 5s default timeout, though it is comfortable when this
 * file runs alone. The fields are plain controlled inputs with nothing per-keystroke to exercise, so the tests that
 * only need them answered set them in one go, and the ones about typing itself still type.
 */
function setValue(input: HTMLElement, value: string) {
  fireEvent.change(input, { target: { value } });
}

/** What the names get filled in with, so a save's payload can be asserted against one list. */
const ANSWERS = {
  rulerName: "Aelara",
  factionName: "The Ember Pact",
  peopleName: "The Vashti",
  demonym: "Vashti",
  cultureName: "Vashti Highland",
  language: "Old Vashti",
};

/**
 * Answers everything the commit gate asks for bar the faction colour, and finishes on the Faction page.
 *
 * Types every name as well as picking the gender, because an undefined slot opens with all six blank - the seeded
 * placeholders used to answer them, leaving only the two pickers to do here. Split out from completeTheForm so a
 * test can leave exactly one answer missing: filling nothing but the gender now strands problems on both pages.
 */
async function fillEverythingButTheColour(user: ReturnType<typeof userEvent.setup>) {
  setValue(rulerNameInput(), ANSWERS.rulerName);
  await user.click(screen.getByRole("radio", { name: "Female" }));

  await user.click(factionTab());
  setValue(factionNameInput(), ANSWERS.factionName);
  setValue(peopleNameInput(), ANSWERS.peopleName);
  setValue(demonymInput(), ANSWERS.demonym);
  setValue(cultureNameInput(), ANSWERS.cultureName);
  setValue(languageInput(), ANSWERS.language);
}

/** Fills in everything the commit gate asks for, leaving the title blank, and finishes on the Faction page. */
async function completeTheForm(user: ReturnType<typeof userEvent.setup>) {
  await fillEverythingButTheColour(user);

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
      rulerName: ANSWERS.rulerName,
      rulerGender: "Female",
      factionName: ANSWERS.factionName,
      factionColour: "#8200db",
      peopleName: ANSWERS.peopleName,
      demonym: ANSWERS.demonym,
      cultureName: ANSWERS.cultureName,
      language: ANSWERS.language,
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

    // Every answer bar the colour is in, and the user is left looking at the page that is not missing one.
    await fillEverythingButTheColour(user);
    await user.click(rulerTab());
    await user.click(saveButton());

    expect(activeTabName()).toContain("Faction");
    expect(screen.getByText("Pick a colour for your faction.")).toBeInTheDocument();
  });

  it("does not save with a blank ruler name", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.clear(rulerNameInput());
    await user.click(saveButton());

    expect(vi.mocked(updatePlayerDefinition)).not.toHaveBeenCalled();
    expect(activeTabName()).toContain("Ruler");
  });
});

describe("the names a game seeds a slot with", () => {
  it("opens every identity field blank, so no seeded placeholder reads as a choice already made", async () => {
    renderDialog();
    await waitForLoad();

    expect(rulerNameInput()).toHaveValue("");
    expect(factionNameInput()).toHaveValue("");
    expect(peopleNameInput()).toHaveValue("");
    expect(demonymInput()).toHaveValue("");
    expect(cultureNameInput()).toHaveValue("");
    expect(languageInput()).toHaveValue("");
  });

  // The gender and the colour are the tell: nothing seeds either, and a save insists on both, so a slot holding
  // them has been defined and its names are the player's own to read back.
  it("keeps the stored names once the slot has been defined", async () => {
    renderDialog(definedSlot());
    await waitFor(() => expect(rulerNameInput()).toHaveValue("Aelara"));

    expect(factionNameInput()).toHaveValue("The Ember Pact");
    expect(peopleNameInput()).toHaveValue("The Vashti");
  });

  // A half-defined slot cannot be reached through this dialog, since a save sets both, but a colour without a gender
  // still stops the names being taken for seeding - the conservative way round, which keeps real answers on screen.
  it("keeps the stored names when only one of the two tells is answered", async () => {
    renderDialog(undefinedSlot({ factionColour: "#8200db" }));

    await waitFor(() => expect(rulerNameInput()).toHaveValue("Player 3"));
  });
});

describe("the people and culture fields", () => {
  // Blank is how the dialog now opens an undefined slot, so clearing a field by hand only stands in for the player
  // having typed something and thought better of it.
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

  it("does not restate the chosen colour in words beneath the picker", async () => {
    const user = userEvent.setup();
    renderDialog();
    await waitForLoad();

    await user.click(factionTab());
    await user.click(colourTrigger());
    await user.click(swatch("purple-700"));

    expect(screen.queryByText(/will be drawn in/i)).not.toBeInTheDocument();
  });

  it("reports a lost colour race on the colour field, on the page that owns it", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(clashError(COLOUR_TAKEN));
    const { onSaved } = renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    // Move away, so landing back on Faction is the dialog's doing rather than where we already were.
    await user.click(rulerTab());
    await user.click(saveButton());

    await waitFor(() => expect(screen.getByText(COLOUR_TAKEN.message)).toBeInTheDocument());
    expect(activeTabName()).toContain("Faction");
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("values a rival took while the dialog was open", () => {
  it("reports every clash at once, so a player is not made to resubmit once per taken value", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(clashError(PEOPLE_TAKEN, DEMONYM_TAKEN, COLOUR_TAKEN));
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(screen.getByText(PEOPLE_TAKEN.message)).toBeInTheDocument());
    expect(screen.getByText(DEMONYM_TAKEN.message)).toBeInTheDocument();
    expect(screen.getByText(COLOUR_TAKEN.message)).toBeInTheDocument();
  });

  it("lands on the Ruler page when that is where a clash is, even though others sit on the Faction page", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(clashError(PORTRAIT_TAKEN, PEOPLE_TAKEN));
    renderDialog();
    await waitForLoad();

    // completeTheForm finishes on the Faction page, so landing on Ruler has to be the dialog's doing.
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(screen.getByText(PORTRAIT_TAKEN.message)).toBeInTheDocument());
    expect(activeTabName()).toContain("Ruler");
  });

  it("clears a clash once the value it was about is edited, rather than leaving a stale message behind", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockRejectedValue(clashError(PEOPLE_TAKEN));
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(screen.getByText(PEOPLE_TAKEN.message)).toBeInTheDocument());

    await user.type(peopleNameInput(), " of the North");

    expect(screen.queryByText(PEOPLE_TAKEN.message)).not.toBeInTheDocument();
  });

  it("falls back to the banner for a 409 carrying no field detail, so the message is never swallowed", async () => {
    const user = userEvent.setup();
    // What the handler answers when it loses the race at the index rather than at its own check: it cannot say which
    // value it was, so there is no field to put it on.
    vi.mocked(updatePlayerDefinition).mockRejectedValue(
      new ApiError("Another player in this game took one of those values while you were choosing. Please try again.", 409, undefined),
    );
    renderDialog();
    await waitForLoad();

    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() =>
      expect(
        screen.getByText("Another player in this game took one of those values while you were choosing. Please try again."),
      ).toBeInTheDocument(),
    );
  });
});

describe("the ruler portrait", () => {
  it("opens on the crown when the slot has not chosen one, which is how a game seeds every slot", async () => {
    renderDialog();
    await waitForLoad();

    expect(portraitTrigger().querySelector("img")).toBeNull();
  });

  // FieldRow draws a hint as an info icon whose accessible name is the hint text, so no such icon is no hint. The
  // colour picker's own hint reads "Colours already taken", which this pattern deliberately does not match.
  it("carries no hint tooltip beside its label", async () => {
    renderDialog();
    await waitForLoad();

    expect(screen.queryByRole("img", { name: /portraits already taken/i })).not.toBeInTheDocument();
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
    vi.mocked(updatePlayerDefinition).mockRejectedValue(clashError(PORTRAIT_TAKEN));
    const { onSaved } = renderDialog();
    await waitForLoad();

    await user.click(portraitTrigger());
    await user.click(portraitTile("Portrait 9"));
    // Finish on the other page, so landing back on Ruler is the dialog's doing rather than where we already were.
    await completeTheForm(user);
    await user.click(saveButton());

    await waitFor(() => expect(screen.getByText(PORTRAIT_TAKEN.message)).toBeInTheDocument());
    expect(activeTabName()).toContain("Ruler");
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("a game already under way", () => {
  /** A fully defined slot in a started game — the only state the lock is ever seen in. */
  function lockedSlot(): PlayerDefinition {
    return definedSlot({ isLocked: true });
  }

  it("says why the fields cannot be touched, rather than leaving the dialog looking broken", async () => {
    renderDialog(lockedSlot());
    await waitFor(() => expect(rulerNameInput()).toHaveValue("Aelara"));

    expect(screen.getByText(/under way, so your ruler's and faction's identity is fixed/)).toBeInTheDocument();
  });

  it("disables every field but the title, which stays editable for the life of the game", async () => {
    renderDialog(lockedSlot());
    await waitFor(() => expect(rulerNameInput()).toHaveValue("Aelara"));

    expect(rulerNameInput()).toBeDisabled();
    expect(screen.getByRole("radio", { name: "Female" })).toBeDisabled();
    expect(portraitTrigger()).toBeDisabled();
    expect(factionNameInput()).toBeDisabled();
    expect(colourTrigger()).toBeDisabled();
    expect(peopleNameInput()).toBeDisabled();
    expect(demonymInput()).toBeDisabled();
    expect(cultureNameInput()).toBeDisabled();
    expect(languageInput()).toBeDisabled();

    expect(rulerTitleInput()).toBeEnabled();
  });

  it("saves a retitle without the user having to re-answer anything", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog(lockedSlot());
    await waitFor(() => expect(rulerNameInput()).toHaveValue("Aelara"));

    await user.clear(rulerTitleInput());
    await user.type(rulerTitleInput(), "Dowager Empress");
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerTitle).toBe("Dowager Empress");
  });

  // A slot from before the people-and-culture columns existed comes back with those blank. The lock means the player
  // cannot fill them in, so validating them would leave a title edit permanently unsaveable.
  it("still saves a retitle on a slot whose stored values would not pass validation", async () => {
    const user = userEvent.setup();
    vi.mocked(updatePlayerDefinition).mockResolvedValue(undefined);
    const { onSaved } = renderDialog(
      undefinedSlot({ isLocked: true, rulerGender: "Female", peopleName: "", demonym: "", cultureName: "", language: "" }),
    );
    await waitForLoad();

    await user.type(rulerTitleInput(), "Steward");
    await user.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePlayerDefinition).mock.calls[0][1].rulerTitle).toBe("Steward");
  });
});

describe("an already-defined slot", () => {
  it("opens on what the player chose last time", async () => {
    renderDialog(definedSlot());

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
