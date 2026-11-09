import { useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Alert, Button, FieldRow, PagedDialog, WholeNumberSlider, errorClass } from "@/components/ui";
import { http } from "@/api/http";
import { useValidatedCommit } from "@/hooks/useValidatedCommit";
import SelectCampaignMap from "@/features/mapgallery/components/SelectCampaignMap";
import { useNewGameSettingsQuery } from "../hooks/useNewGameSettings";

export interface NewGameDialogProps {
  show: boolean;
  onClose: () => void;
  onCreateSuccess: (message?: string) => void;
}

type SectionKey = "general" | "map";

interface SetupGameRequest {
  mapId: number;
  gameName: string;
  numberOfPlayers: number;
  battleMapSize: number;
  generationSeed?: number;
}

/** The choices the player makes, in the shape the validation reads them. */
interface NewGameForm {
  gameName: string;
  selectedMapId: number | null;
  generationSeed: string;
}

type FieldErrors = Partial<Record<keyof NewGameForm, string>>;

/** The form fields the General page holds. A failure on any of these sends the user there; everything else is on the Map page. */
const GENERAL_PAGE_FIELDS: readonly (keyof NewGameForm)[] = ["gameName", "generationSeed"];

/**
 * Validates the form. Pure, so it can run on every render and the caller decides when to reveal the result.
 *
 * Note what is *not* here: neither the player count nor the battle map size can be wrong. Both are dragged on a slider bounded — and, for the size,
 * stepped — by the server's own settings, so no invalid value can be produced in the first place.
 */
function validate(form: NewGameForm): FieldErrors {
  const errors: FieldErrors = {};

  if (form.gameName.trim() === "") {
    errors.gameName = "Give your game a name.";
  }

  if (form.selectedMapId === null) {
    errors.selectedMapId = "Choose the campaign map this game is played on.";
  }

  // A blank seed is a real answer — it asks for a random one — so only a non-blank value that is not a number is a problem.
  if (form.generationSeed.trim() !== "" && isNaN(Number(form.generationSeed.trim()))) {
    errors.generationSeed = "Seed must be a number, or leave blank for a random seed.";
  }

  return errors;
}

export default function NewGameDialog({ show, onClose, onCreateSuccess }: NewGameDialogProps) {
  // The bounds and defaults the form is built from. Fetched rather than hardcoded: these are server configuration, and a second copy here would
  // let the form offer a value the save then refuses. Nothing is requested until the dialog is first opened.
  const settingsQuery = useNewGameSettingsQuery(show);
  const settings = settingsQuery.data;

  const [activeSection, setActiveSection] = useState<SectionKey>("general");
  const [gameName, setGameName] = useState("");
  // Null until the settings land, since the server owns the starting value for both. Neither control is shown while they are null.
  const [numberOfPlayers, setNumberOfPlayers] = useState<number | null>(null);
  const [battleMapSize, setBattleMapSize] = useState<number | null>(null);
  const [generationSeed, setGenerationSeed] = useState("");
  const [mapFilter, setMapFilter] = useState("");
  const [selectedMapId, setSelectedMapId] = useState<number | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!show) {
      return;
    }
    setActiveSection("general");
    setGameName("");
    setNumberOfPlayers(null);
    setBattleMapSize(null);
    setGenerationSeed("");
    setMapFilter("");
    setSelectedMapId(null);
    setErrorMessage(null);
  }, [show]);

  // Seeded the moment the settings arrive, separately from the reset above: the dialog can be opened before the request resolves, and reopening it
  // has to start from the server's own starting values again rather than from the last run's choices. The `?? ` keeps a choice already made in this
  // run — a cached settings object arriving in the same commit as the reset must not stamp over it twice.
  useEffect(() => {
    if (!show || settings == null) {
      return;
    }
    setNumberOfPlayers((current) => current ?? settings.minimumPlayersPerGame);
    setBattleMapSize((current) => current ?? settings.battleMapSettings.defaultBattleMapSize);
  }, [show, settings]);

  const fieldErrors = validate({ gameName, selectedMapId, generationSeed });

  // The settings are a precondition rather than a field: without them there are no bounds to build the two sliders from, so there is nothing for the
  // player to fix and no field to hang a message on. Reported in the banner instead, once Create has been pressed.
  const isSettingsReady = settings != null && numberOfPlayers != null && battleMapSize != null;

  const createMutation = useMutation({
    mutationFn: (payload: SetupGameRequest) => http.post("/games/setup", payload),
  });

  const handleCreate = () => {
    // Safe: the commit gate below only calls this once the settings have landed and the validation passes, which covers all three nulls.
    if (selectedMapId == null || numberOfPlayers == null || battleMapSize == null) {
      return;
    }
    setErrorMessage(null);
    const parsedSeed = generationSeed.trim() !== "" ? Number(generationSeed.trim()) : undefined;
    createMutation.mutate(
      { mapId: selectedMapId, gameName: gameName.trim(), numberOfPlayers, battleMapSize, generationSeed: parsedSeed },
      {
        onSuccess: () => {
          onCreateSuccess(`Game "${gameName.trim()}" created successfully.`);
          onClose();
        },
        onError: (error) => {
          const msg = error instanceof Error ? error.message : "Unknown error";
          setErrorMessage(msg);
        },
      },
    );
  };

  const { submitted, attemptCommit } = useValidatedCommit({
    open: show,
    isValid: isSettingsReady && Object.keys(fieldErrors).length === 0,
    // Send the user to the page holding the first problem; the General page comes first, so it wins ties.
    locate: () => setActiveSection(GENERAL_PAGE_FIELDS.some((field) => fieldErrors[field]) ? "general" : "map"),
    onValid: handleCreate,
  });

  // Validation messages stay hidden until Create has been pressed, so a freshly opened dialog is not pre-littered with "required".
  const shownErrors: FieldErrors = submitted ? fieldErrors : {};

  // A failed save is what the player most needs to see, so it wins the banner; the missing settings only surface once Create has been pressed and
  // there is nothing else to say.
  const bannerMessage = errorMessage ?? (submitted && !isSettingsReady ? "The new game settings have not loaded yet, so a game cannot be created." : null);

  return (
    <PagedDialog<SectionKey>
      open={show}
      title="New Game"
      size="xl"
      // Pinned so the dialog never resizes as the player moves between the two pages, and tall enough for the Map page's table and preview — still
      // the taller of the two, the General page being four label/control rows.
      height={460}
      onCancel={onClose}
      onCommit={attemptCommit}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
      header={
        bannerMessage ? (
          <Alert variant="danger" className="mb-3">
            {bannerMessage}
          </Alert>
        ) : null
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          {/* Never disabled on invalid input: a greyed-out Create leaves the player guessing why. Pressing it is what reveals the problems and
              moves them to the page holding one. */}
          <Button variant="primary" onClick={attemptCommit} disabled={createMutation.isPending}>
            {createMutation.isPending ? "Creating..." : "Create"}
          </Button>
        </>
      }
      sections={[
        {
          key: "general",
          label: "General",
          invalid: GENERAL_PAGE_FIELDS.some((field) => shownErrors[field]),
          content: (
            <div className="flex flex-col gap-2">
              {/* A failed settings request is shown rather than worked around: without the bounds there is no honest set of choices to offer, and
                  pressing Create says so rather than sending a request that cannot be built. */}
              {settingsQuery.isError && (
                <Alert variant="danger">
                  Could not load the new game settings: {settingsQuery.error.message}. A game cannot be created until they load.
                </Alert>
              )}

              <FieldRow label="Game Name" error={shownErrors.gameName}>
                <input
                  className={errorClass(!!shownErrors.gameName)}
                  value={gameName}
                  aria-label="Game Name"
                  placeholder="Enter game name..."
                  maxLength={100}
                  onChange={(e) => setGameName(e.target.value)}
                />
              </FieldRow>

              <FieldRow label="Players">
                {settings == null ? (
                  <span className="text-xs text-slate-500">{settingsQuery.isError ? "Unavailable." : "Loading..."}</span>
                ) : (
                  <WholeNumberSlider
                    value={numberOfPlayers}
                    min={settings.minimumPlayersPerGame}
                    max={settings.maximumPlayersPerGame}
                    unitLabel={numberOfPlayers === 1 ? "Player" : "Players"}
                    ariaLabel="Players"
                    onChange={setNumberOfPlayers}
                  />
                )}
              </FieldRow>

              {/* Both bounds and the step come from the settings, so the knob can only land on a size the save accepts — which is why this row
                  carries no validation message of its own. */}
              <FieldRow label="Battle Map Size" hint="(Width and height of Battle Map)">
                {settings == null ? (
                  <span className="text-xs text-slate-500">{settingsQuery.isError ? "Unavailable." : "Loading..."}</span>
                ) : (
                  <WholeNumberSlider
                    value={battleMapSize}
                    min={settings.battleMapSettings.minimumBattleMapSize}
                    max={settings.battleMapSettings.maximumBattleMapSize}
                    step={settings.battleMapSettings.battleMapSizeIncrement}
                    unitLabel="tiles"
                    ariaLabel="Battle Map Size"
                    onChange={setBattleMapSize}
                  />
                )}
              </FieldRow>

              <FieldRow label="Seed" hint="(Leave blank for a random seed)" error={shownErrors.generationSeed}>
                <input
                  className={errorClass(!!shownErrors.generationSeed)}
                  value={generationSeed}
                  aria-label="Seed"
                  placeholder="(random)"
                  onChange={(e) => setGenerationSeed(e.target.value)}
                />
              </FieldRow>
            </div>
          ),
        },
        {
          key: "map",
          label: "Map & Features",
          invalid: !!shownErrors.selectedMapId,
          content: (
            <div className="flex flex-col gap-2">
              {/* The one thing on this page that can be missing. Shown here rather than in the banner so the message sits beside the table that
                  fixes it, matching the per-field messages on the General page. */}
              {shownErrors.selectedMapId ? <p className="text-xs text-red-600">{shownErrors.selectedMapId}</p> : null}

              <SelectCampaignMap
                enabled={show}
                filter={mapFilter}
                onFilterChange={setMapFilter}
                selectedMapId={selectedMapId}
                onSelectedMapIdChange={setSelectedMapId}
                onConfirmSelection={() => setActiveSection("general")}
              />
            </div>
          ),
        },
      ]}
    />
  );
}
