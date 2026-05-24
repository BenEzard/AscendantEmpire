import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";

interface ResourceManagerTradePageProps {
  selectedTicker: ResourceTickerData | null;
}

export default function ResourceManagerTradePage({ selectedTicker: _ }: ResourceManagerTradePageProps) {
  return <p className="text-sm text-slate-500">Trade details coming soon.</p>;
}
