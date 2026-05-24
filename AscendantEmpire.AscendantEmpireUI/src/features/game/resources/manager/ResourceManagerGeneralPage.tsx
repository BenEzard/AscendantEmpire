import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";

const DEFAULT_DESCRIPTION =
  "A resource used in the production and maintenance of the empire. Detailed records on its origin and applications are held by the imperial Bureau of Commerce.";

interface ResourceManagerGeneralPageProps {
  selectedTicker: ResourceTickerData | null;
}

export default function ResourceManagerGeneralPage({ selectedTicker }: ResourceManagerGeneralPageProps) {
  if (!selectedTicker) {
    return <p className="text-sm text-slate-500">Select a resource to view details.</p>;
  }

  const description = selectedTicker.description ?? DEFAULT_DESCRIPTION;

  return (
    <div>
      <div className="flex items-start justify-between gap-4 mb-3">
        <h2 className="text-xl font-semibold text-slate-800">{selectedTicker.name}</h2>
        {selectedTicker.icon && (
          <div className="shrink-0 [&_img]:w-16 [&_img]:h-16 [&_svg]:w-16 [&_svg]:h-16 [&_svg]:text-slate-700">{selectedTicker.icon}</div>
        )}
      </div>
      <p className="text-sm text-slate-600 leading-relaxed">{description}</p>
    </div>
  );
}
