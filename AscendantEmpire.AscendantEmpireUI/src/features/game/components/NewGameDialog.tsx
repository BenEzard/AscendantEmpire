import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Alert, Button, PagedDialog, Spinner } from "@/components/ui";
import { http } from "@/api/http";
import { searchMapGalleryIndexes } from "@/features/mapgallery/api/mapGalleryApi";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/components/OpenCampaignMapDialog";

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
  const [activeSection, setActiveSection] = useState<SectionKey>("general");
  const [gameName, setGameName] = useState("");
  const [numberOfPlayers, setNumberOfPlayers] = useState(2);
  const [battleMapSize, setBattleMapSize] = useState(50);
  const [generationSeed, setGenerationSeed] = useState("");
  const [mapFilter, setMapFilter] = useState("");
  const [selectedMapId, setSelectedMapId] = useState<number | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [seedError, setSeedError] = useState<string | null>(null);

  useEffect(() => {
    if (!show) return;
    setActiveSection("general");
    setGameName("");
    setNumberOfPlayers(2);
    setBattleMapSize(50);
    setGenerationSeed("");
    setMapFilter("");
    setSelectedMapId(null);
    setErrorMessage(null);
    setSeedError(null);
  }, [show]);

  const {
    data: indexes,
    isLoading: mapsLoading,
    isFetching: mapsFetching,
    error: mapsError,
    refetch,
  } = useQuery<CampaignMapGalleryIndex[], Error>({
    queryKey: ["mapGalleryIndexes"],
    queryFn: ({ signal }) => searchMapGalleryIndexes(signal),
    enabled: show,
    refetchOnWindowFocus: false,
  });

  const filteredMaps = useMemo(() => {
    const list = indexes ?? [];
    const f = mapFilter.trim().toLowerCase();
    if (!f) return list;
    return list.filter((x) => `${x.mapName} ${x.description} ${x.width}x${x.height}`.toLowerCase().includes(f));
  }, [indexes, mapFilter]);

  const selectedMap = useMemo(
    () => (selectedMapId == null ? null : ((indexes ?? []).find((x) => x.campaignMapGalleryIndexId === selectedMapId) ?? null)),
    [indexes, selectedMapId],
  );

  const isSeedValid = generationSeed.trim() === "" || !isNaN(Number(generationSeed.trim()));
  const isBattleMapSizeValid = battleMapSize >= 30 && battleMapSize <= 60;
  const canCreate = gameName.trim().length > 0 && selectedMapId != null && isBattleMapSizeValid && isSeedValid;

  const createMutation = useMutation({
    mutationFn: (payload: SetupGameRequest) => http.post("/games/setup", payload),
  });

  const handleCreate = () => {
    if (!canCreate || selectedMapId == null) return;
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
                <label className="col-span-4 text-sm text-slate-700">
                  Players: <span className="font-semibold">{numberOfPlayers}</span>
                </label>
                <div className="col-span-8">
                  <input
                    type="range"
                    min={2}
                    max={8}
                    step={1}
                    value={numberOfPlayers}
                    onChange={(e) => setNumberOfPlayers(Number(e.target.value))}
                    className="w-full"
                  />
                  <div className="flex justify-between text-xs text-slate-500 mt-1">
                    <span>2</span>
                    <span>3</span>
                    <span>4</span>
                    <span>5</span>
                    <span>6</span>
                    <span>7</span>
                    <span>8</span>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-12 items-center gap-2">
                <label className="col-span-4 text-sm text-slate-700">Battle Map Size</label>
                <div className="col-span-8">
                  <div className="flex items-center gap-3">
                    <input
                      className="w-24 rounded-md border border-slate-300 px-2 py-2 text-sm"
                      type="number"
                      min={30}
                      max={60}
                      step={1}
                      value={battleMapSize}
                      onChange={(e) => {
                        const parsed = parseInt(e.target.value, 10);
                        if (!isNaN(parsed)) setBattleMapSize(parsed);
                      }}
                    />
                    <span className="text-xs text-slate-500">tiles — width &amp; height of battle maps for this game (30–60)</span>
                  </div>
                  {!isBattleMapSizeValid && (
                    <div className="mt-1 text-xs text-[var(--bs-danger)]">Battle map size must be between 30 and 60.</div>
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
          label: (
            <>
              Map
              {selectedMap != null && <span className="ml-2 text-slate-500 font-normal text-xs">— {selectedMap.mapName}</span>}
            </>
          ),
          content: (
            <div className="flex flex-col gap-3">
              <div className="flex items-center gap-2">
                <input
                  className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
                  placeholder="Filter maps by name or description..."
                  value={mapFilter}
                  onChange={(e) => setMapFilter(e.target.value)}
                />
                <Button variant="outline-secondary" onClick={() => refetch()} disabled={mapsLoading || mapsFetching}>
                  Refresh
                </Button>
              </div>

              {mapsError && <Alert variant="danger">Failed to load maps: {mapsError.message}</Alert>}

              {mapsLoading ? (
                <div className="flex items-center gap-2 text-slate-600">
                  <Spinner size="sm" />
                  <span>Loading maps...</span>
                </div>
              ) : (
                <div className="max-h-[320px] overflow-auto rounded-md border border-slate-200">
                  <table className="w-full text-left text-sm">
                    <thead className="sticky top-0 bg-slate-50 text-slate-700">
                      <tr>
                        <th className="px-3 py-2" style={{ width: "45%" }}>
                          Name
                        </th>
                        <th className="px-3 py-2">Description</th>
                        <th className="px-3 py-2" style={{ width: 90 }}>
                          Size
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredMaps.length === 0 ? (
                        <tr>
                          <td colSpan={3} className="px-3 py-3 text-slate-500">
                            No maps found.
                          </td>
                        </tr>
                      ) : (
                        filteredMaps.map((x) => {
                          const isSelected = x.campaignMapGalleryIndexId === selectedMapId;
                          return (
                            <tr
                              key={x.campaignMapGalleryIndexId}
                              className={["border-t border-slate-100 cursor-pointer hover:bg-slate-50", isSelected ? "bg-slate-100" : ""].join(" ")}
                              role="button"
                              onClick={() => setSelectedMapId(x.campaignMapGalleryIndexId)}
                              onDoubleClick={() => {
                                setSelectedMapId(x.campaignMapGalleryIndexId);
                                setActiveSection("general");
                              }}
                              title="Double-click to select and return to General">
                              <td className="px-3 py-2 font-medium">{x.mapName}</td>
                              <td className="px-3 py-2 text-slate-500">{x.description}</td>
                              <td className="px-3 py-2">
                                {x.width}×{x.height}
                              </td>
                            </tr>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          ),
        },
      ]}
    />
  );
}
