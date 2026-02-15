import { Drawer } from "@/components/ui";

interface VisionSettingsOffcanvasProps {
  show: boolean;
  onHide: () => void;
  visionRadius: number;
  setVisionRadius: (radius: number) => void;
  visionLoadWholeMap: boolean;
  setVisionLoadWholeMap: (loadWholeMap: boolean) => void;
}

function coerceNonNegativeInt(value: string): number {
  const next = Math.floor(Number(value));
  return Number.isFinite(next) ? Math.max(0, next) : 0;
}

export default function VisionSettingsOffcanvas({
  show,
  onHide,
  visionRadius,
  setVisionRadius,
  visionLoadWholeMap,
  setVisionLoadWholeMap,
}: VisionSettingsOffcanvasProps) {
  return (
    <Drawer open={show} onClose={onHide} side="left" title="Check Vision Settings">
      <div className="flex flex-col gap-4">
        <div className="grid grid-cols-12 items-center gap-2">
          <label className="col-span-5 text-sm font-medium text-slate-700">Sight Range</label>
          <div className="col-span-7">
            <input
              className="w-full rounded-md border border-slate-300 px-2 py-1 text-sm"
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              value={visionRadius}
              onChange={(e) => setVisionRadius(coerceNonNegativeInt(e.target.value))}
            />
          </div>
        </div>

        <div className="grid grid-cols-12 items-center gap-2">
          <label className="col-span-5 text-sm font-medium text-slate-700">Load Whole Map</label>
          <div className="col-span-7">
            <input type="checkbox" checked={visionLoadWholeMap} onChange={(e) => setVisionLoadWholeMap(e.currentTarget.checked)} />
          </div>
        </div>
      </div>
    </Drawer>
  );
}
