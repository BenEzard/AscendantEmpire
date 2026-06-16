import { useEffect, useState } from "react";
import { FaLocationDot, FaMountain, FaTree } from "react-icons/fa6";
import { Spinner } from "@/components/ui";
import { getCampaignMapPreviewUrl, type CampaignMapPreviewMode } from "@/features/mapgallery/api/mapGalleryApi";

type Props = {
  selectedMapId: number | null;
  selectedMapName?: string;
};

type PreviewStatus = "idle" | "loading" | "loaded" | "missing";

export default function SelectCampaignMapPreview({ selectedMapId, selectedMapName }: Props) {
  const [mode, setMode] = useState<CampaignMapPreviewMode>("biome");
  const [status, setStatus] = useState<PreviewStatus>(selectedMapId == null ? "idle" : "loading");

  useEffect(() => {
    setStatus(selectedMapId == null ? "idle" : "loading");
  }, [selectedMapId, mode]);

  const previewUrl = selectedMapId == null ? null : getCampaignMapPreviewUrl(selectedMapId, mode);
  const buttonClassName = (isActive: boolean) =>
    [
      "inline-flex h-8 w-8 items-center justify-center rounded border text-sm transition-colors",
      isActive ? "border-slate-500 bg-slate-100 text-slate-900" : "border-slate-300 bg-white text-slate-600 hover:bg-slate-50",
    ].join(" ");

  return (
    <div className="flex flex-col rounded-md border border-slate-200 bg-white">
      <div className="flex h-[260px] items-center justify-center bg-slate-50 p-3">
        {selectedMapId == null ? (
          <div className="text-center text-sm text-slate-500">Select a map to preview.</div>
        ) : (
          <div className="relative flex h-full w-full items-center justify-center overflow-hidden rounded border border-slate-200 bg-white">
            {previewUrl != null && (
              <img
                key={previewUrl}
                src={previewUrl}
                alt={`${selectedMapName ?? "Selected map"} ${mode} preview`}
                className={["h-full w-full object-contain object-top transition-opacity", status === "loaded" ? "opacity-100" : "opacity-0"].join(
                  " ",
                )}
                onLoad={() => setStatus("loaded")}
                onError={() => setStatus("missing")}
              />
            )}

            {status === "loading" && (
              <div className="absolute inset-0 flex items-center justify-center gap-2 text-sm text-slate-500">
                <Spinner size="sm" />
                <span>Loading preview...</span>
              </div>
            )}

            {status === "missing" && <div className="absolute inset-0 flex items-center justify-center text-sm text-slate-500">No image found</div>}
          </div>
        )}
      </div>

      <div className="flex items-center justify-start gap-1 border-t border-slate-200 px-3 py-2">
        <button
          type="button"
          onClick={() => setMode("biome")}
          title="Show biome preview"
          aria-pressed={mode === "biome"}
          className={buttonClassName(mode === "biome")}>
          <FaTree aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={() => setMode("geography")}
          title="Show geography preview"
          aria-pressed={mode === "geography"}
          className={buttonClassName(mode === "geography")}>
          <FaMountain aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={() => setMode("startpositions")}
          title="Show start positions"
          aria-pressed={mode === "startpositions"}
          className={buttonClassName(mode === "startpositions")}>
          <FaLocationDot aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}
