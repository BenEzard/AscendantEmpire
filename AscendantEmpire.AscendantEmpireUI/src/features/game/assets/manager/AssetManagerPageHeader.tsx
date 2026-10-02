import { NumberValueField, DeltaIndicator, ProgressBar } from "../../../../components/ui";

interface AssetManagerPageHeaderProps {
  title: string | undefined;
  assetsPerTurn: number;
  amountLastTurn: number;
  currentConsumption: number;
  maxConsumption: number;
}

export default function AssetManagerPageHeader({
  title,
  assetsPerTurn,
  amountLastTurn,
  currentConsumption,
  maxConsumption,
}: AssetManagerPageHeaderProps) {
  return (
    <div className="mb-3 flex items-center justify-between">
      <div className="flex items-center gap-2">
        <NumberValueField actualNumber={assetsPerTurn} tooltip="Amount This Turn" />
        <DeltaIndicator delta={assetsPerTurn - amountLastTurn} tooltip="Change from Last Turn" />
      </div>
      <h3 className="text-sm font-semibold">{title ?? "—"}</h3>
      <div className="flex items-center gap-2">
        <NumberValueField actualNumber={currentConsumption} tooltip="Current Consumption" />
        <ProgressBar
          totalDisplayWidth={40}
          completedAmount={currentConsumption}
          totalAmount={maxConsumption}
          tooltip={`Current vs Maximum Consumption - ${maxConsumption > 0 ? Math.round((currentConsumption / maxConsumption) * 100) : 0}%`}
        />
      </div>
    </div>
  );
}
