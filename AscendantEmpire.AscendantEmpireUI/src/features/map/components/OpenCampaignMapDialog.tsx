import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { Alert, Button, Form, Modal, Spinner, Table } from "react-bootstrap";

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
    <Modal show={show} onHide={onClose} centered size="lg">
      <Modal.Header closeButton>
        <Modal.Title>Open Map</Modal.Title>
      </Modal.Header>

      <Modal.Body>
        <div className="d-flex align-items-center gap-2 mb-3">
          <Form.Control placeholder="Filter maps by name/description..." value={filter} onChange={(e) => setFilter(e.target.value)} />
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
          <div className="d-flex align-items-center gap-2">
            <Spinner animation="border" size="sm" />
            <span>Loading maps...</span>
          </div>
        ) : (
          <div style={{ maxHeight: 420, overflow: "auto" }}>
            <Table hover responsive className="mb-0">
              <thead>
                <tr>
                  <th style={{ width: "40%" }}>Name</th>
                  <th>Description</th>
                  <th style={{ width: 120 }}>Size</th>
                </tr>
              </thead>
              <tbody>
                {filtered.length === 0 ? (
                  <tr>
                    <td colSpan={3} className="text-muted">
                      No maps found.
                    </td>
                  </tr>
                ) : (
                  filtered.map((x) => {
                    const isSelected = x.campaignMapGalleryIndexId === selectedId;
                    return (
                      <tr
                        key={x.campaignMapGalleryIndexId}
                        className={isSelected ? "table-active" : undefined}
                        role="button"
                        onClick={() => setSelectedId(x.campaignMapGalleryIndexId)}
                        onDoubleClick={() => handleRowDoubleClick(x.campaignMapGalleryIndexId)}
                        title="Double-click to open">
                        <td>{x.mapName}</td>
                        <td className="text-muted">{x.description}</td>
                        <td>
                          {x.width}×{x.height}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </Table>
          </div>
        )}
      </Modal.Body>

      <Modal.Footer>
        <Button variant="secondary" onClick={onClose}>
          Cancel
        </Button>

        <Button variant="primary" onClick={handleOpen} disabled={!selected}>
          Open
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
