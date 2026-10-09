import { useState } from "react";
import { CodeField, FieldRow, RangeSlider, inputClass } from "@/components/ui";
import { ExtractionMethod, type MapResourceType } from "@/types/map/MapResource";
import {
  EXTRACTION_METHODS,
  MAP_RESOURCE_KINDS,
  MAP_RESOURCE_KIND_HINT,
  fromPercentSlider,
  kindOf,
  toPercentSlider,
  withExtractionMethod,
  withKind,
  type MapResourceKind,
} from "../../tools/mapResourceDraft";
import { CheckboxRow, PercentInput, PercentSlider } from "./mapResourceFields";

interface MapResourceGeneralSectionProps {
  type: MapResourceType;
  categories: { code: string; displayName: string }[];
  onChange: (updated: MapResourceType) => void;
}

/**
 * The type's own values: identity, category, icon, kind, extraction and discovery. What a growing type does once exhausted is its own page
 * (see MapResourceRenewableSection), since only two of the four kinds have those values at all.
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

      {/* A dropdown rather than a radio per kind, so Kind reads as one row like Category and Extraction above it, and the four
          descriptions travel together in the row's own hint (MAP_RESOURCE_KIND_HINT) instead of an info icon per option. */}
      <FieldRow label="Kind" hint={MAP_RESOURCE_KIND_HINT}>
        <select className={inputClass} value={kind} onChange={(e) => onChange(withKind(type, e.target.value as MapResourceKind))}>
          {MAP_RESOURCE_KINDS.map((option) => (
            <option key={option.kind} value={option.kind}>
              {option.label}
            </option>
          ))}
        </select>
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
        label="Discoverable by default"
        hint="(Off for e.g. uranium, until a tech switches it on)"
        checked={type.isDiscoverableByDefault}
        onChange={(checked) => set("isDiscoverableByDefault", checked)}
      />

      <FieldRow label="Discovery Likelihood" hint={kind === "stratified" ? "(First layer)" : undefined}>
        <PercentSlider value={type.discoveryLikelihood} ariaLabel="Discovery likelihood" onChange={(value) => set("discoveryLikelihood", value)} />
      </FieldRow>

      <CheckboxRow
        label="Requires research to extract"
        checked={type.requiresResearchToExtract}
        onChange={(checked) => set("requiresResearchToExtract", checked)}
      />

      {/* A stratified type judges each layer separately, so its fuzzy factors are on the Layers page.
          One dual-knob slider rather than two percent boxes: the pair is a single range, and the slider keeps the minimum at or below the
          maximum by construction — the ordering the server's validator insists on. It brings its own label row, matching FieldRow's two-column
          grid, so it sits here unwrapped. The knobs read in whole percents, hence the "(%)" on the label. */}
      {kind !== "stratified" ? (
        <RangeSlider
          label="Estimation Fuzziness (%)"
          minValue={toPercentSlider(type.estimationFuzzyFactorMin)}
          maxValue={toPercentSlider(type.estimationFuzzyFactorMax)}
          trackMin={0}
          trackMax={100}
          step={1}
          onChange={({ min, max }) =>
            onChange({ ...type, estimationFuzzyFactorMin: fromPercentSlider(min), estimationFuzzyFactorMax: fromPercentSlider(max) })
          }
        />
      ) : null}
    </div>
  );
}
