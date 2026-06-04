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
}

export default function NewGameDialog({ show, onClose, onCreateSuccess }: NewGameDialogProps) {
  const [activeSection, setActiveSection] = useState<SectionKey>("general");
  const [gameName, setGameName] = useState("");
  const [numberOfPlayers, setNumberOfPlayers] = useState(2);
  const [mapFilter, setMapFilter] = useState("");
  const [selectedMapId, setSelectedMapId] = useState<number | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!show) return;
    setActiveSection("general");
    setGameName("");
    setNumberOfPlayers(2);
    setMapFilter("");
    setSelectedMapId(null);
    setErrorMessage(null);
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

  const canCreate = gameName.trim().length > 0 && selectedMapId != null;

  const createMutation = useMutation({
    mutationFn: (payload: SetupGameRequest) => http.post("/games/setup", payload),
  });

  const handleCreate = () => {
    if (!canCreate || selectedMapId == null) return;
    setErrorMessage(null);
    createMutation.mutate(
      { mapId: selectedMapId, gameName: gameName.trim(), numberOfPlayers },
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
