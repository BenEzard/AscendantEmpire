import { useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Alert, Button, PagedDialog, WholeNumberSlider } from "@/components/ui";
import { http } from "@/api/http";
import SelectCampaignMap from "@/features/mapgallery/components/SelectCampaignMap";
import { useNewGameSettingsQuery } from "@/features/game/hooks/useNewGameSettings";

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
  const [seedError, setSeedError] = useState<string | null>(null);

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
    setSeedError(null);
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

  const isSeedValid = generationSeed.trim() === "" || !isNaN(Number(generationSeed.trim()));
  // Only the typed battle map size can be out of range; the player count is dragged on a slider bounded by the same settings, so it cannot be.
  const isBattleMapSizeValid =
    settings != null &&
    battleMapSize != null &&
    battleMapSize >= settings.battleMapSettings.minimumBattleMapSize &&
    battleMapSize <= settings.battleMapSettings.maximumBattleMapSize;
  const canCreate =
    settings != null && numberOfPlayers != null && gameName.trim().length > 0 && selectedMapId != null && isBattleMapSizeValid && isSeedValid;

  const createMutation = useMutation({
    mutationFn: (payload: SetupGameRequest) => http.post("/games/setup", payload),
  });

  const handleCreate = () => {
    if (!canCreate || selectedMapId == null || numberOfPlayers == null || battleMapSize == null) {
      return;
    }
    // Validate seed before submitting.
    if (!isSeedValid) {
      setSeedError("Seed must be a number, or leave blank for a random seed.");
      return;
    }
    setSeedError(null);
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

  return (
    <PagedDialog
      open={show}
      title="New Game"
      size="lg"
      onCancel={onClose}
      onCommit={canCreate ? handleCreate : undefined}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
      header={
        errorMessage ? (
          <Alert variant="danger" className="mb-3">
            {errorMessage}
          </Alert>
        ) : null
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleCreate} disabled={!canCreate || createMutation.isPending}>
            {createMutation.isPending ? "Creating..." : "Create"}
          </Button>
        </>
      }
      sections={[
        {
          key: "general",
          label: "General",
          content: (
            <div className="flex flex-col gap-4">
              {/* A failed settings request is shown rather than worked around: without the bounds there is no honest set of choices to offer, and
                  Create stays disabled until they arrive. */}
              {settingsQuery.isError && (
                <Alert variant="danger">
                  Could not load the new game settings: {settingsQuery.error.message}. A game cannot be created until they load.
                </Alert>
              )}

              <div className="grid grid-cols-12 items-center gap-2">
                <label className="col-span-4 text-sm text-slate-700">Game Name</label>
                <div className="col-span-8">
                  <input
                    className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                    value={gameName}
                    onChange={(e) => setGameName(e.target.value)}
                    placeholder="Enter game name..."
                    maxLength={100}
                  />
                </div>
              </div>

              <div className="grid grid-cols-12 items-center gap-2">
                <label className="col-span-4 text-sm text-slate-700">Players</label>
                <div className="col-span-8">
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
                </div>
              </div>

              <div className="grid grid-cols-12 items-center gap-2">
                <label className="col-span-4 text-sm text-slate-700">Battle Map Size</label>
                <div className="col-span-8">
                  {settings == null ? (
                    <span className="text-xs text-slate-500">{settingsQuery.isError ? "Unavailable." : "Loading..."}</span>
                  ) : (
                    <>
                      <div className="flex items-center gap-3">
                        <input
                          className="w-24 rounded-md border border-slate-300 px-2 py-2 text-sm"
                          type="number"
                          min={settings.battleMapSettings.minimumBattleMapSize}
                          max={settings.battleMapSettings.maximumBattleMapSize}
                          step={1}
                          value={battleMapSize ?? settings.battleMapSettings.defaultBattleMapSize}
                          onChange={(e) => {
                            const parsed = parseInt(e.target.value, 10);
                            if (!isNaN(parsed)) {
                              setBattleMapSize(parsed);
                            }
                          }}
                        />
                        <span className="text-xs text-slate-500">
                          tiles — width &amp; height of battle maps for this game ({settings.battleMapSettings.minimumBattleMapSize}–
                          {settings.battleMapSettings.maximumBattleMapSize})
                        </span>
                      </div>
                      {!isBattleMapSizeValid && (
                        <div className="mt-1 text-xs text-[var(--bs-danger)]">
                          Battle map size must be between {settings.battleMapSettings.minimumBattleMapSize} and{" "}
                          {settings.battleMapSettings.maximumBattleMapSize}.
                        </div>
                      )}
                    </>
                  )}
                </div>
              </div>

              <div className="grid grid-cols-12 items-center gap-2">
                <label className="col-span-4 text-sm text-slate-700">Seed</label>
                <div className="col-span-8">
                  <div className="flex items-center gap-3">
                    <input
                      className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                      value={generationSeed}
                      onChange={(e) => {
                        setGenerationSeed(e.target.value);
                        setSeedError(null);
                      }}
                      placeholder="(random)"
                    />
                    <span className="text-xs text-slate-500 whitespace-nowrap">Leave blank for a random seed</span>
                  </div>
                  {seedError && <div className="mt-1 text-xs text-[var(--bs-danger)]">{seedError}</div>}
                </div>
              </div>
            </div>
          ),
        },
        {
          key: "map",
          label: "Map & Features",
          content: (
            <SelectCampaignMap
              enabled={show}
              filter={mapFilter}
              onFilterChange={setMapFilter}
              selectedMapId={selectedMapId}
              onSelectedMapIdChange={setSelectedMapId}
              onConfirmSelection={() => setActiveSection("general")}
            />
          ),
        },
      ]}
    />
  );
}
