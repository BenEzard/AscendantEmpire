// DomainSelection — a horizontal, single-select icon tab-strip for choosing a DomainType (Land, Water,
// BelowWater, Air, NearEarthOrbit). Each domain is one icon button with the domain's name as its
// tooltip; exactly one is selected at a time and the choice is emitted via onChange.
//
// This is a thin, game-aware wrapper around the generic ToolbarExclusiveButtons primitive: it supplies
// the domain icons/labels and translates the primitive's string key back to the DomainType union. It's
// a pure controlled component — it holds no state and simply renders `value` and reports changes, so it
// can be dropped into any feature that edits a domain (its selection can drive UI elsewhere).

import ToolbarExclusiveButtons, { type ToolbarExclusiveItem } from "@/components/ui/toolbar/ToolbarExclusiveButtons";

import { DOMAIN_TYPES, type DomainType } from "@/features/unitdesign/types/UnitComponent";
import { domainIcons } from "./domainIcons";
import { DOMAIN_TYPE_LABELS } from "./domainLabels";

interface DomainSelectionProps {
  /** The currently-selected domain. */
  value: DomainType;
  /** Called with the newly-selected domain when the user picks a different button. */
  onChange: (next: DomainType) => void;
  /** Greys out and disables the whole strip. */
  disabled?: boolean;
}

export default function DomainSelection({ value, onChange, disabled = false }: DomainSelectionProps) {
  // One mutually-exclusive icon button per domain, each showing the domain's icon with its label as the
  // tooltip. Built in canonical DOMAIN_TYPES order so the strip always reads the same.
  const items: ToolbarExclusiveItem[] = DOMAIN_TYPES.map((domain) => {
    const Icon = domainIcons[domain];
    return { type: "button", key: domain, icon: <Icon size={18} aria-hidden />, tooltip: DOMAIN_TYPE_LABELS[domain] };
  });

  return (
    <ToolbarExclusiveButtons
      items={items}
      value={value}
      // The primitive works in opaque string keys; every key here is a DomainType, so this cast is safe.
      onChange={(next) => onChange(next as DomainType)}
      disabled={disabled}
    />
  );
}
