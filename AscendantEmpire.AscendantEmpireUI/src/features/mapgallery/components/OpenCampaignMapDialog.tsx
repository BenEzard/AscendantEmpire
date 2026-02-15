import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { Alert, Button, Modal, Spinner } from "@/components/ui";

export interface CampaignMapGalleryIndex {
  campaignMapGalleryIndexId: number;
  mapName: string;
  description: string;
  width: number;
  height: number;
}

export interface OpenCampaignMapDialogProps {
  show: boolean;
  onClose: () => void;

  /**
   * Used by the parent to show a toast message.
   * (Keeping your existing pattern.)
   */
  onSaveSuccess: (message?: string) => void;

  /**
   * Optional callback so the parent can actually "open/load" the map.
   * If you don't need it yet, you can omit it.
   */
  onOpenMap?: (index: CampaignMapGalleryIndex) => void;
}

type SearchResponse = { indexes: CampaignMapGalleryIndex[] };

export default function OpenCampaignMapDialog({ show, onClose, onSaveSuccess, onOpenMap }: OpenCampaignMapDialogProps) {
  const [filter, setFilter] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const fetchIndexes = async () => {
    const res = await axios.get<SearchResponse>("https://localhost:7242/map-gallery/search");
    return res.data.indexes;
  };

  const {
    data: indexes,
    isLoading,
    isFetching,
    error,
    refetch,
  } = useQuery<CampaignMapGalleryIndex[], Error>({
    queryKey: ["mapGalleryIndexes"],
    queryFn: fetchIndexes,
    enabled: show, // only fetch when dialog is shown
    refetchOnWindowFocus: false,
  });

  // Reset selection/filter each time the dialog opens
  useEffect(() => {
    if (!show) return;
    setFilter("");
    setSelectedId(null);
  }, [show]);

  const filtered = useMemo(() => {
    const list = indexes ?? [];
    const f = filter.trim().toLowerCase();
    if (!f) return list;

    return list.filter((x) => {
      const hay = `${x.mapName} ${x.description} ${x.width}x${x.height}`.toLowerCase();
      return hay.includes(f);
    });
  }, [indexes, filter]);

  const selected = useMemo(() => {
    if (selectedId == null) return null;
    return (indexes ?? []).find((x) => x.campaignMapGalleryIndexId === selectedId) ?? null;
  }, [indexes, selectedId]);

  const handleOpen = () => {
    if (!selected) return;

    onOpenMap?.(selected);
    onSaveSuccess(`Opened "${selected.mapName}" (${selected.width}×${selected.height}).`);
    onClose();
  };

  const handleRowDoubleClick = (id: number) => {
    setSelectedId(id);
    // defer open until selected is updated? just open from the found item:
    const item = (indexes ?? []).find((x) => x.campaignMapGalleryIndexId === id);
    if (!item) return;

    onOpenMap?.(item);
    onSaveSuccess(`Opened "${item.mapName}" (${item.width}×${item.height}).`);
    onClose();
  };

  return (
    <Modal
      open={show}
      onClose={onClose}
      title="Open Map"
      size="lg"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleOpen} disabled={!selected}>
            Open
          </Button>
        </>
      }>
      <div className="flex items-center gap-2 mb-3">
        <input
          className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
          placeholder="Filter maps by name/description..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
        <Button variant="outline-secondary" onClick={() => refetch()} disabled={isLoading || isFetching}>
          Refresh
        </Button>
      </div>

      {error && (
        <Alert variant="danger" className="mb-3">
          Failed to load maps: {error.message}
        </Alert>
      )}

      {isLoading ? (
        <div className="flex items-center gap-2 text-slate-600">
          <Spinner size="sm" />
          <span>Loading maps...</span>
        </div>
      ) : (
        <div className="max-h-[420px] overflow-auto rounded-md border border-slate-200">
          <table className="w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-700">
              <tr>
                <th className="px-3 py-2" style={{ width: "40%" }}>
                  Name
                </th>
                <th className="px-3 py-2">Description</th>
                <th className="px-3 py-2" style={{ width: 120 }}>
                  Size
                </th>
              </tr>
            </thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-3 py-3 text-slate-500">
                    No maps found.
                  </td>
                </tr>
              ) : (
                filtered.map((x) => {
                  const isSelected = x.campaignMapGalleryIndexId === selectedId;
                  return (
                    <tr
                      key={x.campaignMapGalleryIndexId}
                      className={["border-t border-slate-100 cursor-pointer hover:bg-slate-50", isSelected ? "bg-slate-100" : ""].join(" ")}
                      role="button"
                      onClick={() => setSelectedId(x.campaignMapGalleryIndexId)}
                      onDoubleClick={() => handleRowDoubleClick(x.campaignMapGalleryIndexId)}
                      title="Double-click to open">
                      <td className="px-3 py-2">{x.mapName}</td>
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
    </Modal>
  );
}
