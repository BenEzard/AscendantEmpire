import { useState } from "react";
import { CircularProgress, DescriptiveSettingDropDown, ToggleSwitch } from "../../../../components/ui";
import type { SelectionOption } from "../../../../components/ui";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import ResourceManagerPageHeader from "./ResourceManagerPageHeader";

interface ResourceManagerTradePageProps {
  selectedTicker: ResourceTickerData | null;
  resourcesPerTurn: number;
  amountLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
  allowTrade?: boolean;
  onAllowTradeChange?: (id: number, value: boolean) => void;
  onTradePartnersChange?: (partners: SelectionOption[]) => void;
}

export default function ResourceManagerTradePage({
  selectedTicker,
  resourcesPerTurn,
  amountLastTurn,
  currentConsumption,
  maxConsumption,
  allowTrade = false,
  onAllowTradeChange,
  onTradePartnersChange,
}: ResourceManagerTradePageProps) {
  const [tradeEnabled, setTradeEnabled] = useState(allowTrade);

  const TRADE_OPTIONS: SelectionOption[] = [
    { id: 1, label: "allies" },
    { id: 3, label: "enemies" },
    { id: 2, label: "neutral parties" },
  ];
  const TRADE_NO_SELECTION: SelectionOption = { id: 0, label: "no one" };
  const TRADE_ALL_SELECTION: SelectionOption = { id: 4, label: "anyone" };

  const [tradePartners, setTradePartners] = useState<SelectionOption[]>(TRADE_OPTIONS);

  function handleTradeToggle(id: number, value: boolean) {
    setTradeEnabled(value);
    onAllowTradeChange?.(id, value);
  }

  return (
    <div>
      <ResourceManagerPageHeader
        title={selectedTicker?.name}
        resourcesPerTurn={resourcesPerTurn}
        amountLastTurn={amountLastTurn}
        currentConsumption={currentConsumption}
        maxConsumption={maxConsumption}
      />
      <div className="flex items-center gap-3 mt-2">
        <span className="text-sm text-slate-500">Allow trade</span>
        <ToggleSwitch id={1} value={tradeEnabled} onChange={handleTradeToggle} inlineLabel="yes-no" />
      </div>
      <p className="text-sm text-slate-500">
        We are willing to import this resource from{" "}
        <DescriptiveSettingDropDown
          multiSelect={true}
          options={TRADE_OPTIONS}
          noSelectionOption={TRADE_NO_SELECTION}
          allSelectionOption={TRADE_ALL_SELECTION}
          finalSeparator=" and "
          value={tradePartners}
          onChange={(partners) => {
            setTradePartners(partners);
            onTradePartnersChange?.(partners);
          }}
        />
        .
      </p>
      <div className="flex items-center gap-3 mt-3">
        <CircularProgress value={0.65} size={56} />
      </div>
    </div>
  );
}
