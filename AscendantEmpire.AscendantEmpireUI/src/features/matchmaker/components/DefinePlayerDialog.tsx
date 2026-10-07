import { useEffect, useMemo, useState } from "react";
import { GiCrown } from "react-icons/gi";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Button,
  ColorPicker,
  DistinctColors,
  FieldRow,
  FreeTextComboBox,
  ImagePicker,
  PagedDialog,
  Spinner,
  errorClass,
  type ImagePickerItem,
  type UsedColor,
  type UsedImage,
} from "@/components/ui";
import { ApiError } from "@/api/http";
import { useValidatedCommit } from "@/hooks/useValidatedCommit";
import { getPlayerDefinition, updatePlayerDefinition } from "../api/matchMakingApi";
import type { PlayerDefinition } from "../types/PlayerDefinition";
import { RULER_GENDERS, titlesFor, type RulerGender } from "../types/RulerGender";
import { RULER_PORTRAITS } from "../types/RulerPortrait";

/**
 * The title the API seeds an undefined slot with, and stores again for a blank one.
 *
 * Mirrors `Player.DEFAULT_RULER_TITLE`. Only ever compared against or shown as placeholder text — the client never
 * sends it, so the server stays the one place that decides what a blank title becomes.
 */
const PLACEHOLDER_RULER_TITLE = "Nobody";

/**
 * The message the API returns when a 409 is about the portrait rather than the colour.
 *
 * Mirrors the string in `UpdatePlayerDefinitionHandler`, the same arrangement as {@link PLACEHOLDER_RULER_TITLE}.
 * Both uniqueness rules come back as a 409 with nothing but a message to tell them apart, and a clash shown against
 * the wrong field would send the player to a page with nothing wrong on it.
 */
const PORTRAIT_TAKEN_MESSAGE = "That portrait has already been taken by another player in this game.";

/** The portraits offered, in the shape the picker takes them. The stored path doubles as the image URL. */
const PORTRAIT_ITEMS: ImagePickerItem[] = RULER_PORTRAITS.map((portrait) => ({ value: portrait.path, label: portrait.label }));

/** The offered paths, for checking that what a slot already holds is still one of them. */
const PORTRAIT_PATHS: ReadonlySet<string> = new Set(RULER_PORTRAITS.map((portrait) => portrait.path));

/** The dialog's two pages. */
type DefinePlayerSection = "ruler" | "faction";

/** The editable form state. Gender and colour start null because a fresh slot asserts neither. */
type Draft = {
  rulerTitle: string;
  rulerName: string;
  rulerGender: RulerGender | null;
  rulerPortraitPath: string;
  factionName: string;
  factionColour: string | null;
  peopleName: string;
  demonym: string;
  cultureName: string;
  language: string;
};

type FieldErrors = Partial<Record<keyof Draft, string>>;

const EMPTY_DRAFT: Draft = {
  rulerTitle: "",
  rulerName: "",
  rulerGender: null,
  rulerPortraitPath: "",
  factionName: "",
  factionColour: null,
  peopleName: "",
  demonym: "",
  cultureName: "",
  language: "",
};

/**
 * react-query key for one game's player definition, so the save can invalidate exactly what it changed.
 *
 * Not exported: nothing outside this dialog reads or writes that cache entry, and a fast-refresh-friendly module here
 * exports only components.
 */
function playerDefinitionQueryKey(gameId: number): unknown[] {
  return ["matchMaking", "playerDefinition", gameId];
}

/**
 * Validates the draft. Pure, so it can run on every render and the caller decides when to reveal the result.
 *
 * Note what is *not* here: the ruler title and the ruler portrait are both optional. A blank title is saved as the
 * placeholder the game seeds a slot with, and no portrait is a real answer rather than a missing one - the crown stands
 * in for it, and it is the one value several players may share. A portrait somebody else has taken is kept out by the
 * picker rather than caught here.
 */
function validate(draft: Draft): FieldErrors {
  const errors: FieldErrors = {};

  if (draft.rulerName.trim() === "") {
    errors.rulerName = "Give your ruler a name.";
  }

  if (draft.rulerGender === null) {
    errors.rulerGender = "Choose a gender for your ruler.";
  }

  if (draft.factionName.trim() === "") {
    errors.factionName = "Give your faction a name.";
  }

  if (draft.factionColour === null) {
    errors.factionColour = "Pick a colour for your faction.";
  }

  if (draft.peopleName.trim() === "") {
    errors.peopleName = "Give your people a name.";
  }

  if (draft.demonym.trim() === "") {
    errors.demonym = "Say what one of your people is called.";
  }

  if (draft.cultureName.trim() === "") {
    errors.cultureName = "Give your people's culture a name.";
  }

  if (draft.language.trim() === "") {
    errors.language = "Name the language your people speak.";
  }

  return errors;
}

export interface DefinePlayerDialogProps {
  open: boolean;

  /** The game whose slot is being defined. The slot itself is resolved server-side from the token. */
  gameId: number;

  /** Closes without saving. The slot stays claimed, carrying whatever the game seeded it with. */
  onCancel: () => void;

  /** Raised after a successful save, so the caller can refresh the lobby and say so. */
  onSaved: () => void;
}

/**
 * The "who are you?" dialog shown once a player has claimed a slot, and reachable again from that slot afterwards.
 *
 * Deliberately fetches its own definition on open rather than taking it from the lobby listing the caller already
 * holds: that listing is a cached react-query result, and a stale reading of the used colours would offer one that has
 * since been taken. A save that loses that race anyway comes back as a 409 and is shown against the colour field.
 */
export default function DefinePlayerDialog({ open, gameId, onCancel, onSaved }: DefinePlayerDialogProps) {
  const queryClient = useQueryClient();

  const [activeSection, setActiveSection] = useState<DefinePlayerSection>("ruler");
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  // A clash on either unique value is only knowable at save time, so both are held apart from the pure field
  // validation - and apart from each other, since they sit on different pages.
  const [colourConflict, setColourConflict] = useState<string | null>(null);
  const [portraitConflict, setPortraitConflict] = useState<string | null>(null);

  const definitionQuery = useQuery<PlayerDefinition, Error>({
    queryKey: playerDefinitionQueryKey(gameId),
    queryFn: () => getPlayerDefinition(gameId),
    enabled: open,
    // Always a fresh reading: the whole point is to know which colours are taken *now*.
    staleTime: 0,
    refetchOnWindowFocus: false,
  });

  const definition = definitionQuery.data;

  // Re-seed whenever the dialog opens (or the fetched definition lands), so a previous session never leaks in.
  useEffect(() => {
    if (!open) {
      return;
    }

    setActiveSection("ruler");
    setColourConflict(null);
    setPortraitConflict(null);

    if (!definition) {
      setDraft(EMPTY_DRAFT);
      return;
    }

    setDraft({
      // The seeded placeholder is shown as an empty field: it is a stand-in, not something to make the player delete.
      rulerTitle: definition.rulerTitle === PLACEHOLDER_RULER_TITLE ? "" : definition.rulerTitle,
      rulerName: definition.rulerName,
      rulerGender: definition.rulerGender,
      // Anything the picker no longer offers - a portrait file since removed or renamed - reads as the crown, so the
      // field never looks unset while holding a path that would be saved straight back.
      rulerPortraitPath: PORTRAIT_PATHS.has(definition.rulerPortraitPath) ? definition.rulerPortraitPath : "",
      factionName: definition.factionName,
      factionColour: definition.factionColour,
      // Shown exactly as stored, like the ruler and faction names beside them: a seeded slot's placeholder is
      // something to edit, and a slot predating these columns comes back blank, which the validation then catches.
      peopleName: definition.peopleName,
      demonym: definition.demonym,
      cultureName: definition.cultureName,
      language: definition.language,
    });
  }, [open, definition]);

  const setField = <K extends keyof Draft>(key: K, value: Draft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  };

  const usedColours = useMemo<UsedColor[]>(
    () => (definition?.usedFactionColours ?? []).map((used) => ({ value: used.value, usedBy: used.usedBy })),
    [definition],
  );

  const usedPortraits = useMemo<UsedImage[]>(
    () => (definition?.usedRulerPortraits ?? []).map((used) => ({ value: used.value, usedBy: used.usedBy })),
    [definition],
  );

  const titleSuggestions = useMemo(() => titlesFor(draft.rulerGender), [draft.rulerGender]);

  const saveMutation = useMutation({
    mutationFn: () =>
      updatePlayerDefinition(gameId, {
        rulerTitle: draft.rulerTitle.trim(),
        rulerName: draft.rulerName.trim(),
        // Safe: validation refuses to commit while the gender is unset.
        rulerGender: draft.rulerGender!,
        rulerPortraitPath: draft.rulerPortraitPath,
        factionName: draft.factionName.trim(),
        factionColour: draft.factionColour!,
        peopleName: draft.peopleName.trim(),
        demonym: draft.demonym.trim(),
        cultureName: draft.cultureName.trim(),
        language: draft.language.trim(),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: playerDefinitionQueryKey(gameId) });
      onSaved();
    },
    onError: (error: Error) => {
      if (error instanceof ApiError && error.status === 409) {
        // Someone took the colour or the portrait between the read and the save. Show it where the fix is, and refetch
        // so whichever tile or swatch lost the race comes back struck through.
        if (error.message === PORTRAIT_TAKEN_MESSAGE) {
          setPortraitConflict(error.message);
          setActiveSection("ruler");
        } else {
          setColourConflict(error.message);
          setActiveSection("faction");
        }

        void queryClient.invalidateQueries({ queryKey: playerDefinitionQueryKey(gameId) });
      }
    },
  });

  const fieldErrors = validate(draft);

  const { submitted, attemptCommit } = useValidatedCommit({
    open,
    isValid: Object.keys(fieldErrors).length === 0,
    // Send the user to the page holding the first problem; the ruler page comes first, so it wins ties.
    locate: () => setActiveSection(fieldErrors.rulerName || fieldErrors.rulerGender ? "ruler" : "faction"),
    onValid: () => {
      setColourConflict(null);
      setPortraitConflict(null);
      saveMutation.mutate();
    },
  });

  const shownErrors: FieldErrors = submitted ? fieldErrors : {};

  // A clash message belongs on its field whether or not a save has been attempted since.
  const colourError = shownErrors.factionColour ?? colourConflict ?? undefined;
  const portraitError = portraitConflict ?? undefined;

  // A failure that is not the colour clash has no field to sit on, so it goes in the banner instead.
  const bannerError =
    saveMutation.error && !(saveMutation.error instanceof ApiError && saveMutation.error.status === 409)
      ? saveMutation.error.message
      : definitionQuery.error?.message;

  return (
    <PagedDialog<DefinePlayerSection>
      open={open}
      title="Define your Ruler and Faction"
      width={720}
      // Tall enough for the Faction page's six rows - still the taller of the two, the Ruler page's portrait being
      // a thumbnail that opens a popover - so neither page scrolls and the dialog never resizes between them.
      height={420}
      onCancel={onCancel}
      onCommit={attemptCommit}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
      header={bannerError ? <Alert variant="danger">{bannerError}</Alert> : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={saveMutation.isPending}>
            Cancel / Close
          </Button>
          {/* Never disabled on invalid input: a greyed-out Save leaves the user guessing why. Pressing it
              is what reveals the problems and moves them to the page holding one. */}
          <Button variant="success" onClick={attemptCommit} disabled={saveMutation.isPending}>
            {saveMutation.isPending ? <Spinner size="sm" /> : "Save"}
          </Button>
        </>
      }
      sections={[
        {
          key: "ruler",
          label: "Ruler",
          invalid: !!(shownErrors.rulerName || shownErrors.rulerGender || portraitError),
          content: definitionQuery.isLoading ? (
            <div className="flex items-center gap-2 text-slate-600">
              <Spinner size="sm" />
              <span>Loading your slot...</span>
            </div>
          ) : (
            <>
              <FieldRow label="Ruler Name" error={shownErrors.rulerName}>
                <input
                  className={errorClass(!!shownErrors.rulerName)}
                  value={draft.rulerName}
                  aria-label="Ruler Name"
                  onChange={(e) => setField("rulerName", e.target.value)}
                />
              </FieldRow>

              {/* Gender sits above the title because it decides which titles are suggested. */}
              <FieldRow label="Ruler Gender" error={shownErrors.rulerGender}>
                <div className="flex items-center gap-4" role="radiogroup" aria-label="Ruler Gender">
                  {RULER_GENDERS.map((gender) => (
                    <label key={gender} className="flex items-center gap-1.5 text-sm text-slate-700">
                      <input
                        type="radio"
                        name="rulerGender"
                        value={gender}
                        checked={draft.rulerGender === gender}
                        onChange={() => setField("rulerGender", gender)}
                      />
                      {gender}
                    </label>
                  ))}
                </div>
              </FieldRow>

              <FieldRow label="Ruler Title" hint="(This will evolve as your empire grows)">
                <FreeTextComboBox
                  value={draft.rulerTitle}
                  onChange={(next) => setField("rulerTitle", next)}
                  suggestions={titleSuggestions}
                  placeholder={`Pick one, type your own, or leave it as "${PLACEHOLDER_RULER_TITLE}"`}
                  ariaLabel="Ruler Title"
                />
              </FieldRow>

              {/* Last on the page because it is the only field that is a picture rather than words, and because
                  gender has to stay directly above the title whose suggestions it drives. */}
              <FieldRow label="Ruler Portrait" hint="(The face your rivals will see; portraits already taken are struck through)" error={portraitError}>
                <ImagePicker
                  value={draft.rulerPortraitPath}
                  onChange={(next) => {
                    setPortraitConflict(null);
                    setField("rulerPortraitPath", next);
                  }}
                  items={PORTRAIT_ITEMS}
                  usedValues={usedPortraits}
                  // Optional, so there has to be something to show for having chosen nothing. The crown is what the
                  // game toolbar already draws for a ruler without a portrait.
                  emptyIcon={<GiCrown size={32} />}
                  columns={5}
                  title="Ruler portrait"
                />
              </FieldRow>
            </>
          ),
        },
        {
          key: "faction",
          label: "Faction",
          invalid: !!(shownErrors.factionName || colourError || shownErrors.peopleName || shownErrors.demonym || shownErrors.cultureName || shownErrors.language),
          content: (
            <>
              <FieldRow label="Faction Name" error={shownErrors.factionName}>
                <input
                  className={errorClass(!!shownErrors.factionName)}
                  value={draft.factionName}
                  aria-label="Faction Name"
                  onChange={(e) => setField("factionName", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Faction Colour" hint="(Colours already taken are struck through)" error={colourError}>
                <ColorPicker
                  value={draft.factionColour}
                  onChange={(next) => {
                    setColourConflict(null);
                    setField("factionColour", next);
                  }}
                  palettes={DistinctColors.palettes()}
                  usedColors={usedColours}
                  columns={DistinctColors.BandSize}
                  swatchSize={22}
                  title="Faction colour"
                />
              </FieldRow>

              {/* The picker's trigger is a small square, so restate the choice in words under it. */}
              {draft.factionColour && (
                <p className="mt-1 grid grid-cols-12 gap-2 text-xs text-slate-500">
                  <span className="col-span-7 col-start-6">Your faction will be drawn in {draft.factionColour}.</span>
                </p>
              )}

              {/* The people behind the faction. Four free-text names with no suggestions behind them: there is no
                  catalogue to draw from, and each one's hint carries the question it answers. */}
              <FieldRow label="People Name" hint="(What do these people call themselves?)" error={shownErrors.peopleName}>
                <input
                  className={errorClass(!!shownErrors.peopleName)}
                  value={draft.peopleName}
                  aria-label="People Name"
                  onChange={(e) => setField("peopleName", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Demonym" hint="(What is one person from this people called?)" error={shownErrors.demonym}>
                <input
                  className={errorClass(!!shownErrors.demonym)}
                  value={draft.demonym}
                  aria-label="Demonym"
                  onChange={(e) => setField("demonym", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Culture Name" hint="(What is the name of their culture?)" error={shownErrors.cultureName}>
                <input
                  className={errorClass(!!shownErrors.cultureName)}
                  value={draft.cultureName}
                  aria-label="Culture Name"
                  onChange={(e) => setField("cultureName", e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Language" hint="(What language do they speak?)" error={shownErrors.language}>
                <input
                  className={errorClass(!!shownErrors.language)}
                  value={draft.language}
                  aria-label="Language"
                  onChange={(e) => setField("language", e.target.value)}
                />
              </FieldRow>
            </>
          ),
        },
      ]}
    />
  );
}
