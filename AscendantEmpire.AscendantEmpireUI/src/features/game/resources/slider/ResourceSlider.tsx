import { useEffect } from "react";
import Slider from "../../../../components/ui/slider/Slider";
import type { SliderProps } from "../../../../components/ui/slider/Slider";
import NumberValueField from "../../../../components/ui/NumberValueField";
import ProgressBar from "../../../../components/ui/ProgressBar";
import { useResourceRemainder } from "./ResourceSliderCollection";
import Tooltip from "@/components/ui/Tooltip";

const VALUE_WIDTH = 56;
const TICK_WIDTH = 24;
const ALLOCATED_TOOLTIP = "Allocated per turn";

export interface ResourceSliderProps extends Omit<SliderProps, "trailing"> {
  /** Total resources added per turn; used to compute the allocated amount displayed beside the groove. */
  resourcesAddedPerTurn?: number;
  maxConsumptionLimit?: number;
  /** Whether this slider is eligible to consume the leftover remainder. */
  canConsumeRemainder?: boolean;
  /** Initial state of whether this slider will consume the remainder. Only used on first render. */
  willConsumeRemainder?: boolean;
}

function RemainderTickButton({ isActive, onClick }: { isActive: boolean; onClick: () => void }) {
  return (
    <div className="flex items-center justify-center" style={{ width: TICK_WIDTH }}>
      <Tooltip content={isActive ? "Consume remainder" : undefined}>
        <button
          type="button"
          onClick={onClick}
          className="flex h-4 w-4 cursor-pointer items-center justify-center"
          aria-label={isActive ? "Remove consume remainder" : "Set as consume remainder"}>
          {isActive && (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-4 w-4 shrink-0 text-emerald-400">
              <path d="M5 12l5 5L20 7" />
            </svg>
          )}
        </button>
      </Tooltip>
    </div>
  );
}

export default function ResourceSlider({
  canConsumeRemainder,
  willConsumeRemainder = false,
  resourcesAddedPerTurn,
  maxConsumptionLimit,
  formatLabel = (v) => `${v}%`,
  ...sliderProps
}: ResourceSliderProps) {
  const remainder = useResourceRemainder();
  const sliderId = String(sliderProps.id);
  const isActiveRemainder = canConsumeRemainder === true && remainder?.activeRemainderId === sliderId;

  useEffect(() => {
    if (!canConsumeRemainder || !remainder) return;
    remainder.registerCandidate(sliderId, willConsumeRemainder);
    return () => remainder.unregisterCandidate(sliderId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sliderId, canConsumeRemainder]);

  const hasTrailing = resourcesAddedPerTurn != null || maxConsumptionLimit != null || canConsumeRemainder != null;

  return (
    <Slider
      {...sliderProps}
      formatLabel={formatLabel}
      trailing={
        hasTrailing
          ? (value) => (
              <>
                {canConsumeRemainder === true ? (
                  <RemainderTickButton isActive={isActiveRemainder} onClick={() => remainder?.requestRemainderToggle(sliderId)} />
                ) : canConsumeRemainder === false ? (
                  <div style={{ width: TICK_WIDTH }} />
                ) : null}
                {resourcesAddedPerTurn != null && (
                  <NumberValueField
                    actualNumber={Math.round((value / 100) * resourcesAddedPerTurn)}
                    tooltip={ALLOCATED_TOOLTIP}
                    width={VALUE_WIDTH}
                  />
                )}
                {maxConsumptionLimit != null &&
                  resourcesAddedPerTurn != null &&
                  (() => {
                    const allocated = Math.round((value / 100) * resourcesAddedPerTurn);
                    return (
                      <ProgressBar
                        totalDisplayWidth={VALUE_WIDTH}
                        completedAmount={allocated}
                        totalAmount={maxConsumptionLimit}
                        tooltip={`Allocated vs Max Consumption (${allocated} / ${maxConsumptionLimit}) — ${maxConsumptionLimit > 0 ? Math.round((allocated / maxConsumptionLimit) * 100) : 0}%`}
                      />
                    );
                  })()}
              </>
            )
          : undefined
      }
    />
  );
}
