import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, PagedDialog, Toast, ToastViewport, ToggleSwitch } from "@/components/ui";
import axios from "axios";

// ── Engine options ──────────────────────────────────────────────────────────

// Must match the MapCreationEngine enum order on the backend (0-based).
export const engineOptions = [
  { value: 0, label: "Blank Map" },
  { value: 1, label: "Perlin Noise V1" },
] as const;

export type EngineValue = (typeof engineOptions)[number]["value"];

// ── Blank-map settings ──────────────────────────────────────────────────────

// Only land biomes make sense as a default (water tiles are auto-assigned).
const BLANK_MAP_BIOMES = [
  { value: "Grassland", label: "Grassland" },
  { value: "Forest", label: "Forest" },
  { value: "Woodland", label: "Woodland" },
  { value: "Jungle", label: "Jungle" },
  { value: "Desert", label: "Desert" },
  { value: "Arid", label: "Arid" },
  { value: "Tundra", label: "Tundra" },
  { value: "Ice", label: "Ice" },
  { value: "Ocean", label: "Ocean" },
] as const;

type BlankMapBiome = (typeof BLANK_MAP_BIOMES)[number]["value"];

interface BlankMapSettings {
  defaultBiome: BlankMapBiome;
}

// ── Perlin-noise settings ───────────────────────────────────────────────────

interface PerlinSettings {
  /** 0–90. Controls what percentage of tiles become water. */
  waterRatio: number;
  /** 0–60. Controls what percentage of land tiles become hills or mountains. */
  hillAndMountainRatio: number;
  /** Leave blank to have the generator pick a random seed. */
  seed: string;
  /** Adds polar ice/tundra at the top and bottom rows. */
  poles: boolean;
  /** Concentrates jungle and arid biomes near the vertical centre of the map. */
  equator: boolean;
  /** Tiles at x=0 match tiles at x=Width-1 so the map wraps east–west. */
  horizontalWrap: boolean;
  /** Tiles at y=0 match tiles at y=Height-1 so the map wraps north–south. */
  verticalWrap: boolean;
}

// ── Shared form state ───────────────────────────────────────────────────────

interface FormState {
  mapName: string;
  description: string;
  width: number;
  height: number;
  engine: EngineValue;
  blank: BlankMapSettings;
  perlin: PerlinSettings;
}

const defaultForm: FormState = {
  mapName: "",
  description: "",
  width: 80,
  height: 60,
  engine: 1, // Perlin Noise V1 as default
  blank: {
    defaultBiome: "Grassland",
  },
  perlin: {
    waterRatio: 55,
    hillAndMountainRatio: 25,
    seed: "",
    poles: true,
    equator: false,
    horizontalWrap: false,
    verticalWrap: false,
  },
};

// ── API shape ───────────────────────────────────────────────────────────────

interface AddCampaignMapRequest {
  mapName: string;
  description: string;
  width: number;
  height: number;
  creationEngine: number;
  parameters?: Record<string, string>;
}

// ── Props ───────────────────────────────────────────────────────────────────

export interface NewCampaignMapDialogProps {
  show: boolean;
  onClose: () => void;
  /**
   * Fired after the map is queued. Carries the processId so the parent can open a
   * progress dialog and poll generation status, plus the map name for the dialog title.
   */
  onSaveSuccess: (result: { processingId: number; mapName: string }) => void;
}

// ── Helpers ─────────────────────────────────────────────────────────────────

/** Returns the parameters dict to pass to the API for the current engine. */
function buildParameters(form: FormState): Record<string, string> {
  if (form.engine === 0) {
    return { defaultBiome: form.blank.defaultBiome };
  }

  const p = form.perlin;
  const params: Record<string, string> = {
    waterRatio: String(p.waterRatio),
    hillAndMountainRatio: String(p.hillAndMountainRatio),
    poles: String(p.poles),
    equator: String(p.equator),
    // Keys must match what AddCampaignMapHandler reads (HorizontalWrap / VerticalWrap).
    HorizontalWrap: String(p.horizontalWrap),
    VerticalWrap: String(p.verticalWrap),
  };

  // Omit seed if blank so the backend generates a random one.
  if (p.seed.trim() !== "") {
    params["seed"] = p.seed.trim();
  }

  return params;
}

// ── Sub-components ──────────────────────────────────────────────────────────

function FieldRow({ label, children, error }: { label: string; children: React.ReactNode; error?: string }) {
  return (
    <>
      <label className="col-span-4 flex items-center text-sm font-medium text-slate-700">{label}</label>
      <div className="col-span-8">
        {children}
        {error && <div className="mt-1 text-xs text-[var(--bs-danger)]">{error}</div>}
      </div>
    </>
  );
}

function TextInput({ value, onChange, placeholder }: { value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <input
      className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
      value={value}
      onChange={(e) => onChange(e.currentTarget.value)}
      placeholder={placeholder}
    />
  );
}

function NumberInput({
  value,
  onChange,
  min,
  max,
  step = 1,
  suffix,
}: {
  value: number;
  onChange: (v: number) => void;
  min?: number;
  max?: number;
  step?: number;
  suffix?: string;
}) {
  return (
    <div className="flex items-center gap-1.5">
      <input
        className="w-24 rounded-md border border-slate-300 px-2 py-1.5 text-sm"
        type="number"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => {
          const parsed = parseInt(e.currentTarget.value, 10);
          if (!isNaN(parsed)) onChange(parsed);
        }}
      />
      {suffix && <span className="text-sm text-slate-500">{suffix}</span>}
    </div>
  );
}

function SectionDivider({ label }: { label: string }) {
  return (
    <div className="col-span-12 mb-1 mt-3 border-b border-slate-200 pb-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</span>
    </div>
  );
}

// ── Main component ──────────────────────────────────────────────────────────

export default function NewCampaignMapDialog({ show, onClose, onSaveSuccess }: NewCampaignMapDialogProps) {
  const [form, setForm] = useState<FormState>({ ...defaultForm });
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Toasts
  const [showErrorToast, setShowErrorToast] = useState(false);
  const [errorItems, setErrorItems] = useState<string[]>([]);

  // Reset form each time the dialog opens.
  useEffect(() => {
    if (show) {
      setForm({ ...defaultForm, perlin: { ...defaultForm.perlin }, blank: { ...defaultForm.blank } });
      setErrors({});
      setShowErrorToast(false);
    }
  }, [show]);

  // ── Typed state setters ────────────────────────────────────────────────────

  function setField<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((s) => ({ ...s, [key]: value }));
  }

  function setPerlin<K extends keyof PerlinSettings>(key: K, value: PerlinSettings[K]) {
    setForm((s) => ({ ...s, perlin: { ...s.perlin, [key]: value } }));
  }

  function setBlank<K extends keyof BlankMapSettings>(key: K, value: BlankMapSettings[K]) {
    setForm((s) => ({ ...s, blank: { ...s.blank, [key]: value } }));
  }

  // ── Validation ─────────────────────────────────────────────────────────────

  const MIN_WIDTH = 10;
  const MIN_HEIGHT = 10;
  const MAX_WIDTH = 600;
  const MAX_HEIGHT = 375;

  function validate(): boolean {
    const e: Record<string, string> = {};

    if (!form.mapName.trim()) e.mapName = "Map name is required.";

    if (form.width < MIN_WIDTH || form.width > MAX_WIDTH) {
      e.width = `Width must be between ${MIN_WIDTH} and ${MAX_WIDTH}.`;
    }
    if (form.height < MIN_HEIGHT || form.height > MAX_HEIGHT) {
      e.height = `Height must be between ${MIN_HEIGHT} and ${MAX_HEIGHT}.`;
    }

    if (form.engine === 1) {
      if (form.perlin.waterRatio < 0 || form.perlin.waterRatio > 90) e.waterRatio = "Water ratio must be 0–90.";
      if (form.perlin.hillAndMountainRatio < 0 || form.perlin.hillAndMountainRatio > 60) e.hillRatio = "Hill & mountain ratio must be 0–60.";
      if (form.perlin.seed.trim() !== "" && isNaN(Number(form.perlin.seed.trim()))) {
        e.seed = "Seed must be a number, or leave blank for a random seed.";
      }
    }

    setErrors(e);
    return Object.keys(e).length === 0;
  }

  // ── API mutation ───────────────────────────────────────────────────────────

  const saveMutation = useMutation({
    mutationFn: async (request: AddCampaignMapRequest) => {
      // Backend returns 201 with { galleryIndexId, processingId }.
      const response = await axios.post<{ galleryIndexId: number; processingId: number }>(
        "https://localhost:7242/map-gallery/add-campaign-map",
        request,
        {
          headers: { "Content-Type": "application/json" },
        },
      );
      return response.data;
    },
    onSuccess: (data) => {
      setShowErrorToast(false);
      onClose();
      // Hand the processId up so the parent can open the generation progress dialog.
      onSaveSuccess({ processingId: data.processingId, mapName: form.mapName.trim() });
    },
    onError: (error: unknown) => {
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
      } else if (error instanceof Error) {
        items = [error.message];
      }

      setErrorItems(items);
      setShowErrorToast(true);
    },
  });

  // ── Save handler ───────────────────────────────────────────────────────────

  function handleSave() {
    if (!validate()) return;
    saveMutation.mutate({
      mapName: form.mapName,
      description: form.description,
      width: form.width,
      height: form.height,
      creationEngine: form.engine,
      parameters: buildParameters(form),
    });
  }

  const isSaving = saveMutation.isPending;

  // ── Page sections ──────────────────────────────────────────────────────────

  const mapInfoSection = (
    <div className="grid grid-cols-12 gap-x-3 gap-y-2">
      <FieldRow label="Map Name" error={errors.mapName}>
        <TextInput value={form.mapName} onChange={(v) => setField("mapName", v)} />
      </FieldRow>

      <FieldRow label="Description" error={errors.description}>
        <textarea
          className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
          rows={3}
          value={form.description}
          onChange={(e) => setField("description", e.currentTarget.value)}
        />
      </FieldRow>

      <FieldRow label="Width" error={errors.width}>
        <NumberInput value={form.width} onChange={(v) => setField("width", v)} min={MIN_WIDTH} max={MAX_WIDTH} suffix="tiles" />
      </FieldRow>

      <FieldRow label="Height" error={errors.height}>
        <NumberInput value={form.height} onChange={(v) => setField("height", v)} min={MIN_HEIGHT} max={MAX_HEIGHT} suffix="tiles" />
      </FieldRow>

      <FieldRow label="Creation Engine">
        <select
          className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
          value={form.engine}
          onChange={(e) => setField("engine", parseInt(e.currentTarget.value, 10) as EngineValue)}>
          {engineOptions.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </FieldRow>
    </div>
  );

  const perlinSettingsSection = (
    <div className="grid grid-cols-12 gap-x-3 gap-y-2">
      <SectionDivider label="Terrain" />

      <FieldRow label="Water Ratio" error={errors.waterRatio}>
        <div className="flex items-center gap-3">
          <NumberInput value={form.perlin.waterRatio} onChange={(v) => setPerlin("waterRatio", v)} min={0} max={90} suffix="%" />
          <span className="text-xs text-slate-500">Percentage of tiles that become water (0–90)</span>
        </div>
      </FieldRow>

      <FieldRow label="Hill & Mountain Ratio" error={errors.hillRatio}>
        <div className="flex items-center gap-3">
          <NumberInput value={form.perlin.hillAndMountainRatio} onChange={(v) => setPerlin("hillAndMountainRatio", v)} min={0} max={60} suffix="%" />
          <span className="text-xs text-slate-500">Percentage of land tiles that become hilly or mountainous (0–60)</span>
        </div>
      </FieldRow>

      <SectionDivider label="Climate" />

      <FieldRow label="Poles">
        <div className="flex items-center gap-3">
          <ToggleSwitch id={1} value={form.perlin.poles} onChange={(_, v) => setPerlin("poles", v)} inlineLabel="yes-no" />
          <span className="text-xs text-slate-500">Add ice and tundra bands at the north and south edges</span>
        </div>
      </FieldRow>

      <FieldRow label="Equator">
        <div className="flex items-center gap-3">
          <ToggleSwitch id={2} value={form.perlin.equator} onChange={(_, v) => setPerlin("equator", v)} inlineLabel="yes-no" />
          <span className="text-xs text-slate-500">Concentrate jungle and arid biomes toward the vertical centre</span>
        </div>
      </FieldRow>

      <SectionDivider label="Wrapping" />

      <FieldRow label="Horizontal Wrap">
        <div className="flex items-center gap-3">
          <ToggleSwitch id={3} value={form.perlin.horizontalWrap} onChange={(_, v) => setPerlin("horizontalWrap", v)} inlineLabel="yes-no" />
          <span className="text-xs text-slate-500">East and west edges connect seamlessly</span>
        </div>
      </FieldRow>

      <FieldRow label="Vertical Wrap">
        <div className="flex items-center gap-3">
          <ToggleSwitch id={4} value={form.perlin.verticalWrap} onChange={(_, v) => setPerlin("verticalWrap", v)} inlineLabel="yes-no" />
          <span className="text-xs text-slate-500">North and south edges connect seamlessly</span>
        </div>
      </FieldRow>

      <SectionDivider label="Advanced" />

      <FieldRow label="Seed" error={errors.seed}>
        <div className="flex items-center gap-3">
          <TextInput value={form.perlin.seed} onChange={(v) => setPerlin("seed", v)} placeholder="(random)" />
          <span className="text-xs text-slate-500">Leave blank for a random seed; the used seed is saved with the map</span>
        </div>
      </FieldRow>
    </div>
  );

  const blankMapSettingsSection = (
    <div className="grid grid-cols-12 gap-x-3 gap-y-2">
      <FieldRow label="Default Biome">
        <select
          className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
          value={form.blank.defaultBiome}
          onChange={(e) => setBlank("defaultBiome", e.currentTarget.value as BlankMapBiome)}>
          {BLANK_MAP_BIOMES.map((b) => (
            <option key={b.value} value={b.value}>
              {b.label}
            </option>
          ))}
        </select>
      </FieldRow>
    </div>
  );

  const settingsSection = form.engine === 1 ? perlinSettingsSection : blankMapSettingsSection;

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <>
      <PagedDialog
        open={show}
        title="New Campaign Map"
        size="lg"
        onCancel={onClose}
        onCommit={handleSave}
        footer={
          <>
            <Button variant="secondary" onClick={onClose} disabled={isSaving}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSave} disabled={isSaving}>
              {isSaving ? "Saving..." : "Save"}
            </Button>
          </>
        }
        sections={[
          { key: "info", label: "Map Info", content: mapInfoSection },
          { key: "settings", label: "Generation Settings", content: settingsSection },
        ]}
      />

      <ToastViewport>
        <Toast
          open={showErrorToast}
          onClose={() => setShowErrorToast(false)}
          variant="danger"
          autoHideMs={6000}
          message={
            <div>
              <div className="font-semibold mb-1">Save Error</div>
              <ul className="list-disc pl-5">
                {errorItems.map((msg, i) => (
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
