import { Slider, SliderGroup, SliderCollection } from "../../../../components/ui/slider";
import { NumberValueField } from "../../../../components/ui";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import tradeIcon from "../../../../assets/icons/trade_40px.png";

interface ResourceManagerDistributionPageProps {
  selectedTicker: ResourceTickerData | null;
  resourcesPerTurn: number;
  amountLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
  resourceSliderMaxConsumption?: number;
}

function TradeIcon() {
  return <img src={tradeIcon} className="h-8 w-8 object-contain" aria-hidden="true" />;
}

function PlanksIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <rect x="2" y="5" width="20" height="4" rx="1" />
      <rect x="2" y="11" width="20" height="4" rx="1" />
      <rect x="2" y="17" width="20" height="4" rx="1" />
    </svg>
  );
}

function CharcoalIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 2C8 7 6 10 6 14a6 6 0 0 0 12 0c0-4-2-7-6-12zm0 18a4 4 0 0 1-4-4c0-2.5 1.2-4.8 4-8.5 2.8 3.7 4 6 4 8.5a4 4 0 0 1-4 4z" />
    </svg>
  );
}

function StrategicStoreIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 2L3 7v10l9 5 9-5V7l-9-5zm0 2.18L19 8.5v7L12 19.82 5 15.5v-7l7-4.32z" />
    </svg>
  );
}

export default function ResourceManagerDistributionPage({
  selectedTicker,
  resourcesPerTurn,
  amountLastTurn,
  currentConsumption,
  maxConsumption,
  resourceSliderMaxConsumption = 0,
}: ResourceManagerDistributionPageProps) {
  return (
    <div>
      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <NumberValueField actualNumber={resourcesPerTurn} tooltip="Amount This Turn" />
          <NumberValueField actualNumber={amountLastTurn} tooltip="Amount Last Turn" />
        </div>
        <h3 className="text-sm font-semibold">{selectedTicker?.name ?? "—"}</h3>
        <div className="flex items-center gap-2">
          <NumberValueField actualNumber={currentConsumption} tooltip="Current Consumption" />
          <NumberValueField actualNumber={maxConsumption} tooltip="Maximum Consumption" />
        </div>
      </div>
      <SliderCollection resourcesAddedPerTurn={resourcesPerTurn}>
        <SliderGroup>
          <Slider
            id={1}
            code="trade"
            icon={<TradeIcon />}
            label="Trade"
            initialValue={30}
            resourcesAddedPerTurn={resourcesPerTurn}
            maxConsumptionLimit={resourceSliderMaxConsumption}
            lockedState="systemlocked"
          />
        </SliderGroup>
        <SliderGroup>
          <Slider
            id={2}
            code="planks"
            icon={<PlanksIcon />}
            label="Planks"
            initialValue={10}
            resourcesAddedPerTurn={resourcesPerTurn}
            maxConsumptionLimit={resourceSliderMaxConsumption}
          />
          <Slider
            id={3}
            code="charcoal"
            icon={<CharcoalIcon />}
            label="Charcoal"
            initialValue={10}
            resourcesAddedPerTurn={resourcesPerTurn}
            maxConsumptionLimit={resourceSliderMaxConsumption}
          />
          <Slider
            id={4}
            code="strategic-store"
            icon={<StrategicStoreIcon />}
            label="Strategic Store"
            initialValue={10}
            resourcesAddedPerTurn={resourcesPerTurn}
            maxConsumptionLimit={resourceSliderMaxConsumption}
          />
        </SliderGroup>
      </SliderCollection>
    </div>
  );
}
