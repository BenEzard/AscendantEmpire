import { cloneElement, isValidElement, type ReactNode } from "react";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import { Badge } from "@/components/ui";

const DEFAULT_DESCRIPTION =
  "A resource used in the production and maintenance of the empire. Detailed records on its origin and applications are held by the imperial Bureau of Commerce.";

interface ResourceManagerGeneralPageProps {
  selectedTicker: ResourceTickerData | null;
}

function getLargeIcon(icon: ReactNode): ReactNode {
  if (isValidElement(icon) && icon.type === "img") {
    const props = icon.props as { src?: string; alt?: string };
    if (props.src?.includes("_40px")) {
      return cloneElement(icon as React.ReactElement<{ src: string }>, {
        src: props.src.replace("_40px", "_300px"),
      });
    }
  }
  return icon;
}

export default function ResourceManagerGeneralPage({ selectedTicker }: ResourceManagerGeneralPageProps) {
  if (!selectedTicker) {
    return <p className="text-sm text-slate-500">Select a resource to view details.</p>;
  }

  const description = selectedTicker.description ?? DEFAULT_DESCRIPTION;

  const badges = [
    // obsoleteIn takes priority — listed first so it appears before demandNotMet
    selectedTicker.obsoleteIn != null && selectedTicker.obsoleteIn > 0 && (
      <Badge
        key="obsolete"
        label={`Obsolete In: ${selectedTicker.obsoleteIn} turns`}
        textColor="#991b1b"
        backgroundColor="#fee2e2"
        tooltipText="This resource is being phased out and will soon become obsolete."
      />
    ),
    selectedTicker.demandNotMet && (
      <Badge
        key="demand"
        label="Demand Not Met"
        textColor="#9a3412"
        backgroundColor="#ffedd5"
        tooltipText="Current demand exceeds available supply."
      />
    ),
    selectedTicker.isLuxury && (
      <Badge
        key="luxury"
        label="Luxury"
        textColor="#4c1d95"
        backgroundColor="#ede9fe"
        tooltipText="A luxury resource, valued for prestige and trade."
      />
    ),
    selectedTicker.isImported && (
      <Badge
        key="imported"
        label="Imported"
        textColor="#1e40af"
        backgroundColor="#dbeafe"
        tooltipText="This resource is sourced from outside the empire."
      />
    ),
    selectedTicker.isExported && (
      <Badge
        key="exported"
        label="Exported"
        textColor="#065f46"
        backgroundColor="#d1fae5"
        tooltipText="This resource is exported to foreign markets."
      />
    ),
    selectedTicker.hasStrategicStockpile && (
      <Badge
        key="stockpile"
        label="Strategic Stockpile"
        textColor="#1c1917"
        backgroundColor="#e7e5e4"
        tooltipText="A strategic reserve of this resource is maintained."
      />
    ),
  ].filter(Boolean);

  return (
    <div className="flex gap-4">
      <div className="flex-1">
        <h2 className="text-xl font-semibold text-slate-800 mb-2">{selectedTicker.name}</h2>
        {badges.length > 0 && <div className="flex flex-wrap gap-1.5 mb-3">{badges}</div>}
        <p className="text-sm text-slate-600 leading-relaxed">{description}</p>
      </div>
      {selectedTicker.icon && (
        <div className="shrink-0 [&_img]:w-24 [&_img]:h-24 [&_svg]:w-24 [&_svg]:h-24 [&_svg]:text-slate-700">{getLargeIcon(selectedTicker.icon)}</div>
      )}
    </div>
  );
}
