import { useState } from "react";
import { CodeField, FieldRow, HintIcon, inputClass } from "@/components/ui";
import { ExtractionMethod, type MapResourceType } from "@/types/map/MapResource";
import { EXTRACTION_METHODS, MAP_RESOURCE_KINDS, grows, kindOf, withExtractionMethod, withKind } from "../../tools/mapResourceDraft";
import { CheckboxRow, PercentInput, WholeNumberInput } from "./mapResourceFields";

interface MapResourceGeneralSectionProps {
  type: MapResourceType;
  categories: { code: string; displayName: string }[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * The type's own values: identity, category, icon, kind, extraction, discovery, and (for a type that grows) how long it survives exhaustion.
 * Values the chosen kind does not use are hidden, and switching kind clears them (see withKind), so what is saved always suits the kind.
 */
export default function MapResourceGeneralSection({ type, categories, onChange }: MapResourceGeneralSectionProps) {
  const kind = kindOf(type);
  const isSaved = type.id != null;

  // Hidden rather than broken when the path does not resolve yet (a designer typing it, or an image not yet deployed). Reset when the path changes.
  const [iconFailedFor, setIconFailedFor] = useState<string | null>(null);
  const showIcon = type.iconPath != null && type.iconPath !== iconFailedFor;

  const set = <K extends keyof MapResourceType>(field: K, value: MapResourceType[K]) => onChange({ ...type, [field]: value });

  return (
    <div className="w-full max-w-xl">
      <FieldRow label="Name">
        <input className={inputClass} value={type.displayName} onChange={(e) => set("displayName", e.target.value)} />
      </FieldRow>

      {/* The code is the key saves match on, so once saved it is fixed: changing it would add a second type rather than rename this one. */}
      <FieldRow label="Code" hint={isSaved ? "(Fixed once saved)" : undefined}>
        {isSaved ? (
          <input className={`${inputClass} bg-slate-100 text-slate-500`} value={type.code} readOnly />
        ) : (
          <CodeField value={type.code} source={type.displayName} onChange={(code) => set("code", code)} />
        )}
      </FieldRow>

      <FieldRow label="Category">
        <select className={inputClass} value={type.mapResourceCategoryCode} onChange={(e) => set("mapResourceCategoryCode", e.target.value)}>
          {categories.map((category) => (
            <option key={category.code} value={category.code}>
              {category.displayName}
            </option>
          ))}
        </select>
      </FieldRow>

      {/* Sort Order is not edited here: the server places a new type at the end of its category, and the order is changed in the
          database by hand. It still orders the picker, so the draft carries it through untouched. */}

      <FieldRow label="Map Icon" hint="(Image path; empty for none)">
        <div className="flex items-center gap-2">
          <input
            className={inputClass}
            value={type.iconPath ?? ""}
            placeholder="e.g. /images/map-resources/berry.png"
            onChange={(e) => set("iconPath", e.target.value === "" ? null : e.target.value)}
          />
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded border border-slate-200 bg-slate-50">
            {showIcon ? <img src={type.iconPath!} alt="" className="max-h-8 max-w-8" onError={() => setIconFailedFor(type.iconPath)} /> : null}
          </span>
        </div>
      </FieldRow>

      <FieldRow label="Kind" align="top">
        <div role="radiogroup" aria-label="Kind" className="space-y-1">
          {MAP_RESOURCE_KINDS.map((option) => (
            // Each kind's description hangs off an info icon beside its name, so the four options stay one line
            // each and read the same way as the hints on the rows above.
            <label key={option.kind} className="flex items-center gap-2 text-sm text-slate-700">
              <input type="radio" name="map-resource-kind" checked={kind === option.kind} onChange={() => onChange(withKind(type, option.kind))} />
              <span className="inline-flex items-center">
                {option.label}
                <HintIcon hint={option.description} className="ml-1 align-middle" />
              </span>
            </label>
          ))}
        </div>
      </FieldRow>

      <FieldRow label="Extraction">
        <select
          className={inputClass}
          value={type.extractionMethod}
          onChange={(e) => onChange(withExtractionMethod(type, Number(e.target.value) as ExtractionMethod))}>
          {EXTRACTION_METHODS.map((method) => (
            <option key={method.value} value={method.value}>
              {method.label}
            </option>
          ))}
        </select>
      </FieldRow>

      {type.extractionMethod === ExtractionMethod.Capture ? (
        <FieldRow label="Capture Likelihood">
          <PercentInput value={type.captureLikelihood} onChange={(value) => set("captureLikelihood", value)} />
        </FieldRow>
      ) : null}

      <CheckboxRow
        label="Requires research to extract"
        checked={type.requiresResearchToExtract}
        onChange={(checked) => set("requiresResearchToExtract", checked)}
      />

      <CheckboxRow
        label="Discoverable by default"
        hint="(Off for e.g. uranium, until a tech switches it on)"
        checked={type.isDiscoverableByDefault}
        onChange={(checked) => set("isDiscoverableByDefault", checked)}
      />

      <FieldRow label="Discovery Likelihood" hint={kind === "stratified" ? "(First layer)" : undefined}>
        <PercentInput value={type.discoveryLikelihood} required onChange={(value) => set("discoveryLikelihood", value)} />
      </FieldRow>

      {/* A stratified type judges each layer separately, so its fuzzy factors are on the Layers page. */}
      {kind !== "stratified" ? (
        <FieldRow label="Estimation Fuzziness" hint="(Minimum to maximum)">
          <div className="grid grid-cols-2 gap-2">
            <PercentInput value={type.estimationFuzzyFactorMin} ariaLabel="Fuzziness minimum" onChange={(value) => set("estimationFuzzyFactorMin", value)} />
            <PercentInput value={type.estimationFuzzyFactorMax} ariaLabel="Fuzziness maximum" onChange={(value) => set("estimationFuzzyFactorMax", value)} />
          </div>
        </FieldRow>
      ) : null}

      {grows(kind) ? (
        <>
          <FieldRow label="Survival Rounds" hint="(Dormant, once exhausted)">
            <WholeNumberInput value={type.survivalRounds} onChange={(value) => set("survivalRounds", value)} />
          </FieldRow>

          <FieldRow label="Reseed Likelihood" hint="(Each dormant Round)">
            <PercentInput value={type.reseedLikelihood} onChange={(value) => set("reseedLikelihood", value)} />
          </FieldRow>
        </>
      ) : null}
    </div>
  );
}
