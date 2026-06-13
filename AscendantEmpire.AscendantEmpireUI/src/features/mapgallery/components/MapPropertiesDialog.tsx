import { useEffect, useState } from "react";
import { Alert, Button, Modal } from "@/components/ui";
import { useCampaignMapStartPositionsQuery } from "@/features/mapgallery/hooks/useCampaignMapStartPositions";
import { useUpdateMapPropertiesMutation } from "@/features/mapgallery/hooks/useUpdateMapPropertiesMutation";
import type { CampaignMapGalleryIndex } from "@/features/mapgallery/components/OpenCampaignMapDialog";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type Props = {
  show: boolean;
  mapIndex: CampaignMapGalleryIndex | null;
  onClose: () => void;
  /** Called with the saved name and description so the parent can update openMapIndex. */
  onSaveSuccess: (updatedName: string, updatedDescription: string | null) => void;
  onFailureToast: (message: string) => void;
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function MapPropertiesDialog({ show, mapIndex, onClose, onSaveSuccess, onFailureToast }: Props) {
  const [mapName, setMapName] = useState("");
  const [description, setDescription] = useState("");
  const [nameError, setNameError] = useState("");
  const [serverErrors, setServerErrors] = useState<string[]>([]);

  // Reset form fields each time the dialog opens.
  useEffect(() => {
    if (show && mapIndex != null) {
      setMapName(mapIndex.mapName);
      setDescription(mapIndex.description ?? "");
      setNameError("");
      setServerErrors([]);
    }
  }, [show, mapIndex]);

  // Load the start positions count for the info label.
  const { data: startPositionsResponse } = useCampaignMapStartPositionsQuery(
    mapIndex?.campaignMapGalleryIndexId ?? null,
    show && mapIndex != null,
  );

  const startLocationCount = startPositionsResponse?.startPositions.length ?? 0;

  const updateMutation = useUpdateMapPropertiesMutation({
    onSuccess: (updatedName, updatedDescription) => {
      onSaveSuccess(updatedName, updatedDescription);
      onClose();
    },
    onFailureToast: (message) => {
      setServerErrors([message]);
    },
  });

  // ---------------------------------------------------------------------------
  // Validation + save
  // ---------------------------------------------------------------------------

  function handleSave() {
    const trimmedName = mapName.trim();

    if (!trimmedName) {
      setNameError("Map name is required.");
      return;
    }

    setNameError("");
    setServerErrors([]);

    updateMutation.mutate({
      id: mapIndex!.campaignMapGalleryIndexId,
      mapName: trimmedName,
      // Treat blank description as null.
      description: description.trim() || null,
    });
  }

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  const isPending = updateMutation.isPending;

  return (
    <Modal
      open={show}
      title="Map Properties"
      onClose={() => {
        if (!isPending) {
          onClose();
        }
      }}
      size="md"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isPending}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleSave} disabled={isPending}>
            {isPending ? "Saving…" : "Save"}
          </Button>
        </>
      }>
      <div className="grid grid-cols-12 gap-x-4 gap-y-3">
        {serverErrors.length > 0 && (
          <div className="col-span-12">
            <Alert variant="danger">
              {serverErrors.map((e, i) => (
                <div key={i}>{e}</div>
              ))}
            </Alert>
          </div>
        )}

        {/* Name */}
        <label className="col-span-3 flex items-center text-sm font-medium text-slate-700">Name</label>
        <div className="col-span-9">
          <input
            className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
            value={mapName}
            onChange={(e) => setMapName(e.currentTarget.value)}
            placeholder="Map name"
            disabled={isPending}
          />
          {nameError && <div className="mt-1 text-xs text-[var(--bs-danger)]">{nameError}</div>}
        </div>

        {/* Description */}
        <label className="col-span-3 flex items-start pt-1.5 text-sm font-medium text-slate-700">Description</label>
        <div className="col-span-9">
          <textarea
            className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.currentTarget.value)}
            placeholder="Optional description"
            disabled={isPending}
          />
        </div>

        {/* Start Locations count */}
        <label className="col-span-3 flex items-center text-sm font-medium text-slate-700">Start Locations</label>
        <div className="col-span-9 flex items-center text-sm text-slate-600">{startLocationCount}</div>
      </div>
    </Modal>
  );
}
