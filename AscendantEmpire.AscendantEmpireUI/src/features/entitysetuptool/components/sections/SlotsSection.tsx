// SlotsSection — the dialog's "Slots" page. A component can expose any number of slots for fitting
// other components, and each slot carries 1+ acceptance rules that decide what may be fitted.
//
// This is a fully controlled editor: it reads the slots array and emits a new array on every edit. No
// working-row translation is needed here (unlike the damage-profile editors) because the slot model is
// already the exact shape we keep — so we mutate the arrays directly.

import EditableTable, { type EditableTableColumn } from "@/components/ui/EditableTable";

import {
  COMPONENT_TYPES,
  SLOT_RULE_OPERATORS,
  SLOT_RULE_TYPES,
  createEmptySlot,
  createEmptySlotRule,
  type SlotRuleOperator,
  type SlotRuleType,
  type UnitComponentSlot,
  type UnitComponentSlotRule,
} from "../../types/UnitComponent";
import { FieldRow, NumberField, inputClass } from "../componentSetupFields";

interface SlotsSectionProps {
  slots: UnitComponentSlot[];
  onChange: (next: UnitComponentSlot[]) => void;
}

export default function SlotsSection({ slots, onChange }: SlotsSectionProps) {
  // Replace the slot at `index` with `next`; add/remove operate on the whole array.
  const updateSlot = (index: number, next: UnitComponentSlot) => {
    onChange(slots.map((slot, i) => (i === index ? next : slot)));
  };

  const addSlot = () => {
    onChange([...slots, createEmptySlot()]);
  };

  const removeSlot = (index: number) => {
    onChange(slots.filter((_, i) => i !== index));
  };

  return (
    <div className="w-full max-w-xl">
      <p className="mb-2 text-xs text-slate-400">
        Slots let this component carry other fitted components. Each slot accepts only components that satisfy all of its rules.
      </p>

      {slots.length === 0 ? (
        <p className="mb-3 text-sm text-slate-500">No slots yet.</p>
      ) : (
        slots.map((slot, index) => (
          // Index keys are fine here: the card holds no local state, every field is controlled.
          <SlotCard
            key={index}
            slot={slot}
            index={index}
            onChange={(next) => updateSlot(index, next)}
            onRemove={() => removeSlot(index)}
          />
        ))
      )}

      <button
        type="button"
        className="mt-1 rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
        onClick={addSlot}>
        + Add Slot
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// One slot card: the slot's own fields plus its rules table.
// ---------------------------------------------------------------------------

function SlotCard({
  slot,
  index,
  onChange,
  onRemove,
}: {
  slot: UnitComponentSlot;
  index: number;
  onChange: (next: UnitComponentSlot) => void;
  onRemove: () => void;
}) {
  // Patch one field of this slot.
  const patch = <K extends keyof UnitComponentSlot>(field: K, value: UnitComponentSlot[K]) => {
    onChange({ ...slot, [field]: value });
  };

  // Rule helpers — edit the slot's rules array.
  const updateRule = (ruleIndex: number, next: UnitComponentSlotRule) => {
    patch("rules", slot.rules.map((rule, i) => (i === ruleIndex ? next : rule)));
  };

  const addRule = () => {
    patch("rules", [...slot.rules, createEmptySlotRule()]);
  };

  const removeRule = (ruleIndex: number) => {
    patch("rules", slot.rules.filter((_, i) => i !== ruleIndex));
  };

  const ruleColumns: EditableTableColumn<UnitComponentSlotRule>[] = [
    {
      header: "Match",
      className: "w-32",
      renderCell: (rule, ruleIndex) => (
        <select
          className={inputClass}
          value={rule.ruleType}
          onChange={(e) => updateRule(ruleIndex, { ...rule, ruleType: e.target.value as SlotRuleType })}>
          {SLOT_RULE_TYPES.map((ruleType) => (
            <option key={ruleType} value={ruleType}>
              {ruleType === "ComponentType" ? "Component Type" : "Has Tag"}
            </option>
          ))}
        </select>
      ),
    },
    {
      header: "Operator",
      className: "w-28",
      renderCell: (rule, ruleIndex) => (
        <select
          className={inputClass}
          value={rule.operator}
          onChange={(e) => updateRule(ruleIndex, { ...rule, operator: e.target.value as SlotRuleOperator })}>
          {SLOT_RULE_OPERATORS.map((operator) => (
            <option key={operator} value={operator}>
              {operator}
            </option>
          ))}
        </select>
      ),
    },
    {
      header: "Value",
      renderCell: (rule, ruleIndex) => (
        <RuleValueField rule={rule} onChange={(value) => updateRule(ruleIndex, { ...rule, value })} />
      ),
    },
  ];

  return (
    <div className="mb-3 rounded-md border border-slate-200 p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-sm font-semibold text-slate-700">Slot {index + 1}</span>
        <button
          type="button"
          className="text-xs text-slate-400 hover:text-[var(--bs-danger)]"
          onClick={onRemove}>
          Remove slot
        </button>
      </div>

      <FieldRow label="Label" hint="(optional)">
        <input
          className={inputClass}
          value={slot.displayName ?? ""}
          placeholder="e.g. Primary Mount"
          // Empty box means "no label" (null), matching the nullable backend column.
          onChange={(e) => patch("displayName", e.target.value === "" ? null : e.target.value)}
        />
      </FieldRow>

      <FieldRow label="Visible / Locked">
        <div className="flex items-center gap-4 text-sm text-slate-700">
          <label className="flex items-center gap-1.5">
            <input type="checkbox" checked={slot.isVisible} onChange={(e) => patch("isVisible", e.target.checked)} />
            Visible
          </label>
          <label className="flex items-center gap-1.5">
            <input type="checkbox" checked={slot.isLocked} onChange={(e) => patch("isLocked", e.target.checked)} />
            Locked
          </label>
        </div>
      </FieldRow>

      <FieldRow label="UI Position" hint="(x, y)">
        <div className="flex items-center gap-2">
          <NumberField value={slot.uiXPosition} onChange={(next) => patch("uiXPosition", next)} />
          <NumberField value={slot.uiYPosition} onChange={(next) => patch("uiYPosition", next)} />
        </div>
      </FieldRow>

      <div className="mt-2 mb-1 text-xs font-medium text-slate-500">Acceptance Rules</div>
      <EditableTable
        columns={ruleColumns}
        rows={slot.rules}
        onAddRow={addRule}
        onRemoveRow={removeRule}
        addLabel="Rule"
        emptyHint="No rules — this slot would accept nothing. Add at least one."
      />
    </div>
  );
}

// The rule value is a ComponentType name, a comma-separated list of them, or a tag code, depending on
// the rule. For the common single-ComponentType case we offer a dropdown; otherwise a free-text input
// (comma-separated for `In`, or an arbitrary tag code for HasTag).
function RuleValueField({ rule, onChange }: { rule: UnitComponentSlotRule; onChange: (value: string) => void }) {
  if (rule.ruleType === "ComponentType" && rule.operator !== "In") {
    return (
      <select className={inputClass} value={rule.value} onChange={(e) => onChange(e.target.value)}>
        {/* Placeholder kept selectable so an as-yet-unset rule shows a prompt rather than a silent default. */}
        <option value="">Select…</option>
        {COMPONENT_TYPES.map((type) => (
          <option key={type} value={type}>
            {type}
          </option>
        ))}
      </select>
    );
  }

  const placeholder = rule.ruleType === "HasTag" ? "tag code, e.g. stealth" : "comma-separated types, e.g. Weapon,Radar";
  return <input className={inputClass} value={rule.value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />;
}
