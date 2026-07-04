// SlotsSection — the dialog's "Slots" page. A component can expose any number of slots for fitting
// other components, and each slot carries 1+ acceptance rules that decide what may be fitted.
//
// This is a fully controlled editor: it reads the slots array and emits a new array on every edit. No
// working-row translation is needed here (unlike the damage-profile editors) because the slot model is
// already the exact shape we keep — so we mutate the arrays directly.

import { useEffect, useMemo, useRef, useState } from "react";
import { FaChevronDown, FaChevronRight, FaPencil } from "react-icons/fa6";
import { HiTrash } from "react-icons/hi2";

import EditableTable, { type EditableTableColumn } from "@/components/ui/EditableTable";
import Tooltip from "@/components/ui/Tooltip";

import {
  SLOT_MATCHABLE_COMPONENT_TYPES,
  SLOT_RULE_OPERATORS,
  SLOT_RULE_TYPES,
  createEmptySlot,
  createEmptySlotRule,
  type SlotRuleOperator,
  type SlotRuleType,
  type UnitComponentSlot,
  type UnitComponentSlotRule,
} from "@/features/unitdesign/types/UnitComponent";
import { FieldRow, NumberField, inputClass } from "../componentSetupFields";

// A smaller-text, tighter-left-padding variant of the shared inputClass, scoped to the rules table's
// Match/Operator/Value cells only — they sit in a dense three-column row where the normal text-sm reads
// oversized and the default px-2 leaves too much space before the text.
const ruleFieldClass = inputClass.replace("text-sm", "text-xs").replace("px-2", "pl-1 pr-2");

// Equals/NotEquals read as symbols in the tight Operator column; In stays spelled out (no obvious symbol).
function operatorLabel(operator: SlotRuleOperator): string {
  switch (operator) {
    case "Equals":
      return "=";
    case "NotEquals":
      return "!=";
    default:
      return operator;
  }
}

interface SlotsSectionProps {
  slots: UnitComponentSlot[];
  onChange: (next: UnitComponentSlot[]) => void;
}

export default function SlotsSection({ slots, onChange }: SlotsSectionProps) {
  // Collapse state is lifted up here (rather than local to each SlotCard) so addSlot can collapse every
  // existing card at once. Keyed by array index, same as the slots array itself. Starts with every
  // existing slot collapsed so the page opens compact rather than dumping every card's fields at once.
  const [collapsedIndices, setCollapsedIndices] = useState<Set<number>>(() => new Set(slots.map((_, i) => i)));

  // Replace the slot at `index` with `next`; add/remove operate on the whole array.
  const updateSlot = (index: number, next: UnitComponentSlot) => {
    onChange(slots.map((slot, i) => (i === index ? next : slot)));
  };

  const addSlot = () => {
    // Collapse every existing slot so the newly-added one (expanded by default) stands out.
    setCollapsedIndices(new Set(slots.map((_, i) => i)));
    onChange([...slots, createEmptySlot()]);
  };

  const removeSlot = (index: number) => {
    onChange(slots.filter((_, i) => i !== index));
    // Shift collapse state down to match the post-removal indices, dropping the removed slot's entry.
    setCollapsedIndices((prev) => {
      const next = new Set<number>();
      prev.forEach((i) => {
        if (i < index) {
          next.add(i);
        } else if (i > index) {
          next.add(i - 1);
        }
      });
      return next;
    });
  };

  const toggleCollapsed = (index: number) => {
    setCollapsedIndices((prev) => {
      const next = new Set(prev);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }
      return next;
    });
  };

  return (
    <div className="w-full max-w-xl">
      {slots.length === 0 ? (
        <p className="mb-3 text-sm text-slate-500">No slots yet.</p>
      ) : (
        // space-y (not a per-card margin) so the gap sits only between cards — the button below controls
        // its own gap to the last card independently via its own margin-top.
        <div className="space-y-2">
          {slots.map((slot, index) => (
            // Index keys are fine here: the card holds no local state, every field is controlled.
            <SlotCard
              key={index}
              slot={slot}
              index={index}
              isCollapsed={collapsedIndices.has(index)}
              onToggleCollapsed={() => toggleCollapsed(index)}
              onChange={(next) => updateSlot(index, next)}
              onRemove={() => removeSlot(index)}
            />
          ))}
        </div>
      )}

      <button
        type="button"
        className="mt-1.5 rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
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
  isCollapsed,
  onToggleCollapsed,
  onChange,
  onRemove,
}: {
  slot: UnitComponentSlot;
  index: number;
  isCollapsed: boolean;
  onToggleCollapsed: () => void;
  onChange: (next: UnitComponentSlot) => void;
  onRemove: () => void;
}) {
  // Patch one field of this slot.
  const patch = <K extends keyof UnitComponentSlot>(field: K, value: UnitComponentSlot[K]) => {
    onChange({ ...slot, [field]: value });
  };

  // Falls back to "Slot N" while no custom label has been set, matching the old static header text.
  const fallbackLabel = `Slot ${index + 1}`;

  // The header label toggles between display text and an input on click; collapsing (state lifted to
  // SlotsSection, see onToggleCollapsed) hides everything below the header so only the label (plus the
  // header controls) remains visible.
  const [isEditingLabel, setIsEditingLabel] = useState(false);
  const labelInputRef = useRef<HTMLInputElement>(null);

  // Focus (and select) the input as soon as we enter edit mode.
  useEffect(() => {
    if (isEditingLabel) {
      labelInputRef.current?.focus();
      labelInputRef.current?.select();
    }
  }, [isEditingLabel]);

  // Rule helpers — edit the slot's rules array.
  const updateRule = (ruleIndex: number, next: UnitComponentSlotRule) => {
    patch(
      "rules",
      slot.rules.map((rule, i) => (i === ruleIndex ? next : rule)),
    );
  };

  const addRule = () => {
    patch("rules", [...slot.rules, createEmptySlotRule()]);
  };

  const removeRule = (ruleIndex: number) => {
    patch(
      "rules",
      slot.rules.filter((_, i) => i !== ruleIndex),
    );
  };

  const ruleColumns: EditableTableColumn<UnitComponentSlotRule>[] = [
    {
      header: "Match",
      className: "w-32 pl-1",
      renderCell: (rule, ruleIndex) => (
        <select
          className={ruleFieldClass}
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
      className: "w-28 pl-1",
      renderCell: (rule, ruleIndex) => (
        <select
          className={ruleFieldClass}
          value={rule.operator}
          onChange={(e) => updateRule(ruleIndex, { ...rule, operator: e.target.value as SlotRuleOperator })}>
          {SLOT_RULE_OPERATORS.map((operator) => (
            <option key={operator} value={operator}>
              {operatorLabel(operator)}
            </option>
          ))}
        </select>
      ),
    },
    {
      header: "Value",
      className: "pl-1",
      renderCell: (rule, ruleIndex) => <RuleValueField rule={rule} onChange={(value) => updateRule(ruleIndex, { ...rule, value })} />,
    },
  ];

  return (
    <div className="rounded-md border border-slate-200 px-3 py-1.5">
      <div className={`flex items-center justify-between gap-2 ${isCollapsed ? "" : "mb-2"}`}>
        {isEditingLabel ? (
          <input
            ref={labelInputRef}
            // -ml-2 cancels out inputClass's px-2 so the text lines up with the other left-aligned text
            // on the card (e.g. "Acceptance Rules"); the padding itself stays, for the height-matching
            // reason below.
            className={`${inputClass} -ml-2 text-sm font-semibold text-slate-700`}
            value={slot.displayName ?? ""}
            placeholder={fallbackLabel}
            // Empty box means "no label" (null), matching the nullable backend column.
            onChange={(e) => patch("displayName", e.target.value === "" ? null : e.target.value)}
            onBlur={() => setIsEditingLabel(false)}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === "Escape") {
                e.currentTarget.blur();
              }
            }}
          />
        ) : (
          <span
            role="button"
            tabIndex={0}
            // Matches the edit-mode input's border + padding (with a transparent border) so the box is
            // exactly the same height in both states — otherwise entering edit mode grows the row and
            // shifts everything below it down. -ml-2 cancels the padding back out so the text still
            // lines up with the card's other left-aligned text.
            className="-ml-2 inline-flex min-w-0 cursor-text items-center gap-1 rounded-md border border-transparent px-2 py-2 text-sm font-semibold text-slate-700 hover:text-slate-900"
            onClick={() => setIsEditingLabel(true)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                setIsEditingLabel(true);
              }
            }}>
            <span className="truncate">{slot.displayName || fallbackLabel}</span>
            <FaPencil size={11} className="shrink-0 text-slate-400" aria-hidden />
          </span>
        )}

        <div className="flex shrink-0 items-center gap-2">
          <Tooltip content="Remove Slot">
            <button
              type="button"
              aria-label={`Remove ${slot.displayName || fallbackLabel}`}
              className="text-slate-400 hover:text-[var(--bs-danger)]"
              onClick={onRemove}>
              <HiTrash size={16} aria-hidden />
            </button>
          </Tooltip>
          <button
            type="button"
            aria-label={isCollapsed ? "Expand slot" : "Collapse slot"}
            aria-expanded={!isCollapsed}
            className="text-slate-400 hover:text-slate-600"
            onClick={onToggleCollapsed}>
            {isCollapsed ? <FaChevronRight size={14} aria-hidden /> : <FaChevronDown size={14} aria-hidden />}
          </button>
        </div>
      </div>

      {!isCollapsed && (
        <>
          <FieldRow label="Accessibility">
            <div className="flex items-center gap-4 text-sm text-slate-700">
              <label className="flex items-center gap-1.5">
                <input type="checkbox" checked={slot.isVisible} onChange={(e) => patch("isVisible", e.target.checked)} />
                Visible
              </label>
              <label
                className="flex items-center gap-1.5"
                title="Starts locked for every player; a player must unlock it in-game (e.g. via tech) before they can fit anything into it.">
                <input type="checkbox" checked={slot.isLocked} onChange={(e) => patch("isLocked", e.target.checked)} />
                Locked
              </label>
            </div>
          </FieldRow>

          {/* Normalized offset from the component image's center (0 = center, ±1 = edges). Usually set by
              dragging on the Image page, but editable here for fine-tuning — hence the fractional step. */}
          <FieldRow label="UI Position" hint="(x, y from center)">
            <div className="flex items-center gap-2">
              <NumberField value={slot.uiXPosition} step={0.01} onChange={(next) => patch("uiXPosition", next)} />
              <NumberField value={slot.uiYPosition} step={0.01} onChange={(next) => patch("uiYPosition", next)} />
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
        </>
      )}
    </div>
  );
}

// The rule value is a ComponentType name, a comma-separated list of them, or a tag code, depending on
// the rule. For the common single-ComponentType case we offer a dropdown; ComponentType+In gets a
// checklist (see ComponentTypeInField); everything else (HasTag, of any operator) is still free text.
function RuleValueField({ rule, onChange }: { rule: UnitComponentSlotRule; onChange: (value: string) => void }) {
  if (rule.ruleType === "ComponentType" && rule.operator === "In") {
    return <ComponentTypeInField value={rule.value} onChange={onChange} />;
  }

  if (rule.ruleType === "ComponentType") {
    return (
      // appearance-none + a hand-drawn chevron (rather than the native select arrow) so this control's
      // left inset exactly matches ComponentTypeInField's button below — native <select> chrome adds its
      // own OS-drawn padding on top of ours, which made the Value field's left edge jump when switching
      // between Equals/NotEquals (a <select>) and In (a <button>).
      <div className="relative">
        <select
          className={`${ruleFieldClass} appearance-none pr-5`}
          value={rule.value}
          onChange={(e) => onChange(e.target.value)}>
          {/* Placeholder kept selectable so an as-yet-unset rule shows a prompt rather than a silent default. */}
          <option value="">Select…</option>
          {SLOT_MATCHABLE_COMPONENT_TYPES.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </select>
        <FaChevronDown className="pointer-events-none absolute right-1.5 top-1/2 -translate-y-1/2 text-slate-400" size={9} aria-hidden />
      </div>
    );
  }

  const placeholder = rule.ruleType === "HasTag" ? "tag code, e.g. stealth" : "comma-separated types, e.g. Weapon,Radar";
  return <input className={ruleFieldClass} value={rule.value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />;
}

// ComponentType + In: a checklist over SLOT_MATCHABLE_COMPONENT_TYPES instead of free-text CSV entry. The rule's
// `value` stays a plain comma-separated string on the wire (matching SlotRuleEvaluator's SplitValues on
// the backend) — this component just parses it in and re-serialises it out on every toggle.
function ComponentTypeInField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  const selected = useMemo(() => new Set(value.split(",").map((v) => v.trim()).filter(Boolean)), [value]);

  // Close on outside click, same pattern as DropButton.
  useEffect(() => {
    if (!open) return;

    const onPointerDown = (e: PointerEvent) => {
      if (rootRef.current && e.target instanceof Node && !rootRef.current.contains(e.target)) {
        setOpen(false);
      }
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    return () => document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
  }, [open]);

  const toggle = (type: string) => {
    const next = new Set(selected);
    if (next.has(type)) {
      next.delete(type);
    } else {
      next.add(type);
    }
    // Walk SLOT_MATCHABLE_COMPONENT_TYPES rather than the Set so the serialised CSV order is stable and readable.
    onChange(SLOT_MATCHABLE_COMPONENT_TYPES.filter((type) => next.has(type)).join(","));
  };

  const summary = selected.size === 0 ? "Select…" : SLOT_MATCHABLE_COMPONENT_TYPES.filter((type) => selected.has(type)).join(", ");

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        className={`${ruleFieldClass} flex items-center justify-between gap-1 text-left`}
        onClick={() => setOpen((prev) => !prev)}
        aria-haspopup="listbox"
        aria-expanded={open}>
        <span className="truncate">{summary}</span>
        <FaChevronDown className="shrink-0 text-slate-400" size={10} aria-hidden />
      </button>

      {open && (
        <div className="absolute left-0 top-full z-50 mt-1 min-w-full rounded-md border border-slate-200 bg-white p-1 shadow-sm">
          {SLOT_MATCHABLE_COMPONENT_TYPES.map((type) => (
            <label
              key={type}
              className="flex items-center gap-1.5 whitespace-nowrap rounded px-2 py-1 text-xs text-slate-700 hover:bg-slate-100">
              <input type="checkbox" checked={selected.has(type)} onChange={() => toggle(type)} />
              {type}
            </label>
          ))}
        </div>
      )}
    </div>
  );
}
