import { SliderGroup } from "../../../../components/ui/slider";
import { AssetSlider, AssetSliderCollection } from "../slider";
import type { SliderSnapshot } from "../slider";
import type { AssetTickerData } from "../tickers/AssetTickerCollection";
import AssetManagerPageHeader from "./AssetManagerPageHeader";

interface AssetManagerDistributionPageProps {
  selectedTicker: AssetTickerData | null;
  assetsPerTurn: number;
  amountLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
  onSliderChange?: (sliders: SliderSnapshot[]) => void;
  onRemainderChange?: (sliderId: number | null) => void;
}

export default function AssetManagerDistributionPage({
  selectedTicker,
  assetsPerTurn,
  amountLastTurn,
  currentConsumption,
  maxConsumption,
  onSliderChange,
  onRemainderChange,
}: AssetManagerDistributionPageProps) {
  const distributionConfig = selectedTicker?.distributionConfig;

  return (
    <div>
      <AssetManagerPageHeader
        title={selectedTicker?.name}
        assetsPerTurn={assetsPerTurn}
        amountLastTurn={amountLastTurn}
        currentConsumption={currentConsumption}
        maxConsumption={maxConsumption}
      />
      {distributionConfig ? (
        <AssetSliderCollection
          key={selectedTicker?.id ?? "none"}
          assetsAddedPerTurn={assetsPerTurn}
          onChange={onSliderChange}
          onRemainderChange={onRemainderChange}>
          {distributionConfig.sliderGroups.map((group, groupIndex) => (
            <SliderGroup key={groupIndex}>
              {group.sliders.map((slider) => (
                <AssetSlider
                  key={slider.id}
                  id={slider.id}
                  code={slider.code}
                  icon={slider.icon}
                  label={slider.label}
                  initialValue={slider.initialValue}
                  assetsAddedPerTurn={assetsPerTurn}
                  maxConsumptionLimit={slider.maxConsumptionLimit}
                  lockedState={slider.lockedState}
                  canConsumeRemainder={slider.canConsumeRemainder}
                  willConsumeRemainder={slider.willConsumeRemainder}
                />
              ))}
            </SliderGroup>
          ))}
        </AssetSliderCollection>
      ) : (
        <p className="text-sm text-slate-500">No distribution configuration available.</p>
      )}
    </div>
  );
}
