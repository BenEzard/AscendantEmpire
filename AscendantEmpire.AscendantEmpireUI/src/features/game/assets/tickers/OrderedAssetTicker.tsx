import AssetTicker, { type AssetTickerProps } from "./AssetTicker";

export interface OrderedAssetTickerProps extends AssetTickerProps {
  draggable?: boolean;
  onDragHandlePointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  isDragSource?: boolean;
  isDropTarget?: boolean;
}

export default function OrderedAssetTicker({
  draggable = false,
  onDragHandlePointerDown,
  isDragSource,
  isDropTarget,
  disabled,
  name,
  ...rest
}: OrderedAssetTickerProps) {
  return (
    <div
      className={[
        "ordered-asset-ticker",
        draggable ? "ordered-asset-ticker--draggable" : "",
        isDragSource ? "ordered-asset-ticker--drag-source" : "",
        isDropTarget ? "ordered-asset-ticker--drop-target" : "",
      ]
        .filter(Boolean)
        .join(" ")}>
      <AssetTicker name={name} disabled={disabled} onPointerDown={draggable ? onDragHandlePointerDown : undefined} {...rest} />
    </div>
  );
}
