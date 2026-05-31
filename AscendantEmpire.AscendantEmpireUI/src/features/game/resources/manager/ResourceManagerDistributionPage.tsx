import { SliderGroup } from "../../../../components/ui/slider";
import { ResourceSlider, ResourceSliderCollection } from "../slider";
import type { SliderSnapshot } from "../slider";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import ResourceManagerPageHeader from "./ResourceManagerPageHeader";

interface ResourceManagerDistributionPageProps {
  selectedTicker: ResourceTickerData | null;
  resourcesPerTurn: number;
  amountLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
  onSliderChange?: (sliders: SliderSnapshot[]) => void;
  onRemainderChange?: (sliderId: number | null) => void;
}

export default function ResourceManagerDistributionPage({
  selectedTicker,
  resourcesPerTurn,
  amountLastTurn,
  currentConsumption,
  maxConsumption,
  onSliderChange,
  onRemainderChange,
}: ResourceManagerDistributionPageProps) {
  const distributionConfig = selectedTicker?.distributionConfig;

  return (
    <div>
      <ResourceManagerPageHeader
        title={selectedTicker?.name}
        resourcesPerTurn={resourcesPerTurn}
        amountLastTurn={amountLastTurn}
        currentConsumption={currentConsumption}
        maxConsumption={maxConsumption}
      />
      {distributionConfig ? (
        <ResourceSliderCollection
          key={selectedTicker?.id ?? "none"}
          resourcesAddedPerTurn={resourcesPerTurn}
          onChange={onSliderChange}
          onRemainderChange={onRemainderChange}>
          {distributionConfig.sliderGroups.map((group, groupIndex) => (
            <SliderGroup key={groupIndex}>
              {group.sliders.map((slider) => (
                <ResourceSlider
                  key={slider.id}
                  id={slider.id}
                  code={slider.code}
                  icon={slider.icon}
                  label={slider.label}
                  initialValue={slider.initialValue}
                  resourcesAddedPerTurn={resourcesPerTurn}
                  maxConsumptionLimit={slider.maxConsumptionLimit}
                  lockedState={slider.lockedState}
                  canConsumeRemainder={slider.canConsumeRemainder}
                  willConsumeRemainder={slider.willConsumeRemainder}
                />
              ))}
            </SliderGroup>
          ))}
        </ResourceSliderCollection>
      ) : (
        <p className="text-sm text-slate-500">No distribution configuration available.</p>
      )}
    </div>
  );
}
