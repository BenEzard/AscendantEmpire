import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Modal, Toast, ToastViewport } from "@/components/ui";
import axios from "axios";

// --- String-based engine names ---
export const engineOptions = ["BlankMap", "PerlinV1"] as const;
export type MapCreationEngine = (typeof engineOptions)[number];

export interface NewCampaignMapValues {
  id: number | null; // null for new records
  mapName: string;
  description: string;
  width: number;
  height: number;
  creationEngine: number;
  properties: string; // JSON or free text; up to you
}

export interface AddCampaignMapRequest {
  id?: number | null /** null/undefined for create; number for update */;
  mapName: string;
  description: string;
  width: number;
  height: number;
  creationEngine: number;
  parameters?: Record<string, string>;
}

export interface NewCampaignMapDialogProps {
  show: boolean;
  initialValues?: Partial<NewCampaignMapValues>;
  onClose: () => void;
  onSaveSuccess: (message?: string) => void;
}

const emptyValues: NewCampaignMapValues = {
  id: null,
  mapName: "",
  description: "",
  width: 10,
  height: 10,
  creationEngine: 0,
  properties: "{}",
};

export default function NewCampaignMapDialog({ show, initialValues, onClose, onSaveSuccess }: NewCampaignMapDialogProps) {
  const [campaignMap, setCampaignMap] = useState<NewCampaignMapValues>({ ...emptyValues });
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Toasts
  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorToastItems, setErrorToastItems] = useState<string[]>([]);
  const toggleErrorToast = () => setShowErrorToast(!showErrorToast);

  useEffect(() => {
    if (show) {
      setCampaignMap({ ...emptyValues, ...(initialValues || {}) } as NewCampaignMapValues);
      setErrors({});
      setSaving(false);
      setShowErrorToast(false);
    }
  }, [show, initialValues]);

  function set<K extends keyof NewCampaignMapValues>(key: K, val: NewCampaignMapValues[K]) {
    setCampaignMap((s) => ({ ...s, [key]: val }));
  }

  const MIN_WIDTH = 10;
  const MIN_HEIGHT = 10;
  const MAX_WIDTH = 600;
  const MAX_HEIGHT = 375;

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!campaignMap.mapName?.trim()) e.mapName = "Map name is required.";
    if (!campaignMap.description?.trim()) e.desciption = "Description is required.";
    if (campaignMap.width < MIN_WIDTH || campaignMap.width > MAX_WIDTH) {
      e.width = `Width must be between ${MIN_WIDTH} and ${MAX_WIDTH}.`;
    }
    if (campaignMap.height < MIN_HEIGHT || campaignMap.height > MAX_HEIGHT) {
      e.height = `Height must be between ${MIN_HEIGHT} and ${MAX_HEIGHT}.`;
    }

    // If you expect JSON in properties, validate softly
    try {
      if (campaignMap.properties?.trim()) {
        JSON.parse(campaignMap.properties);
      }
    } catch {
      e.properties = "Properties must be valid JSON (or leave as {}).";
    }

    setErrors(e);
    return Object.keys(e).length === 0;
  }

  const saveCampaignMapMutation = useMutation({
    mutationFn: async (request: AddCampaignMapRequest) => {
      const response = await axios.post("https://localhost:7242/map-gallery/add-campaign-map", request, {
        headers: { "Content-Type": "application/json" },
      });

      return response.data; // response.data contains parsed JSON body
    },
    onSuccess: () => {
      // make sure the error toast is hidden
      setShowErrorToast(false);

      onClose();
      onSaveSuccess("Campaign Map saved successfully."); // notify parent
    },
    onError: (error: any) => {
      let items: string[] = ["An unknown error occurred."];

      if (axios.isAxiosError(error)) {
        const data = error.response?.data;
        if (Array.isArray(data?.errors)) {
          items = data.errors as string[];
        } else if (typeof data === "string") {
          items = [data];
        } else if (data?.message) {
          items = [data.message as string];
        } else if (error.message) {
          items = [error.message as string];
        }
      } else if (error?.message) {
        items = [error.message as string];
      }

      setErrorToastItems(items);
      toggleErrorToast();
      console.error("Save error:", error);
    },
  });

  async function handleSave() {
    if (!validate()) return;
    setSaving(true);
    try {
      const request: AddCampaignMapRequest = {
        id: campaignMap.id ?? undefined,
        mapName: campaignMap.mapName,
        description: campaignMap.description,
        width: campaignMap.width,
        height: campaignMap.height,
        creationEngine: campaignMap.creationEngine,
        // optional: if your API wants parameters from the JSON field
        // safely parse; you've already validated it's JSON
        parameters: JSON.parse(campaignMap.properties || "{}"),
      };
      saveCampaignMapMutation.mutate(request);
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <Modal
        open={show}
        onClose={onClose}
        title="New Campaign Map"
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSave} disabled={saving}>
              {saving ? "Saving..." : "Save"}
            </Button>
          </>
        }>
        <div className="grid grid-cols-12 gap-3">
          <label className="col-span-3 text-sm font-medium text-slate-700">Id</label>
          <div className="col-span-9">
            <input
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={campaignMap.id ?? ""}
              placeholder="(auto)"
              disabled
              readOnly
            />
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Map Name</label>
          <div className="col-span-9">
            <input
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={campaignMap.mapName}
              onChange={(e) => set("mapName", e.currentTarget.value)}
            />
            {errors.mapName && <div className="mt-1 text-xs text-[var(--bs-danger)]">{errors.mapName}</div>}
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Width</label>
          <div className="col-span-9">
            <input
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              type="number"
              min={MIN_WIDTH}
              max={MAX_WIDTH}
              value={campaignMap.width}
              onChange={(e) => set("width", parseInt(e.currentTarget.value || "0", 10))}
            />
            {errors.width && <div className="mt-1 text-xs text-[var(--bs-danger)]">{errors.width}</div>}
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Height</label>
          <div className="col-span-9">
            <input
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              type="number"
              min={MIN_HEIGHT}
              max={MAX_HEIGHT}
              value={campaignMap.height}
              onChange={(e) => set("height", parseInt(e.currentTarget.value || "0", 10))}
            />
            {errors.height && <div className="mt-1 text-xs text-[var(--bs-danger)]">{errors.height}</div>}
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Creation Engine</label>
          <div className="col-span-9">
            <select
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              value={campaignMap.creationEngine}
              onChange={(e) => set("creationEngine", parseInt(e.currentTarget.value || "0", 10))}>
              {engineOptions.map((o, i) => (
                <option key={o} value={i}>
                  {o}
                </option>
              ))}
            </select>
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Description</label>
          <div className="col-span-9">
            <textarea
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              rows={3}
              value={campaignMap.description}
              onChange={(e) => set("description", e.currentTarget.value)}
            />
          </div>

          <label className="col-span-3 text-sm font-medium text-slate-700">Properties (JSON)</label>
          <div className="col-span-9">
            <textarea
              className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
              rows={12}
              value={campaignMap.properties}
              onChange={(e) => set("properties", e.currentTarget.value)}
            />
            {errors.properties && <div className="mt-1 text-xs text-[var(--bs-danger)]">{errors.properties}</div>}
          </div>
        </div>
      </Modal>

      <ToastViewport>
        <Toast
          open={showErrorToast}
          onClose={toggleErrorToast}
          variant="danger"
          autoHideMs={5000}
          message={
            <div>
              <div className="font-semibold mb-1">Save Error</div>
              <ul className="list-disc pl-5">
                {errorToastItems.map((msg, i) => (
                  <li key={i}>{msg}</li>
                ))}
              </ul>
            </div>
          }
        />
      </ToastViewport>
    </>
  );
}
