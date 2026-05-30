import ResourceTicker, { type ResourceTickerProps } from "./ResourceTicker";

export interface OrderedResourceTickerProps extends ResourceTickerProps {
  draggable?: boolean;
  onDragHandlePointerDown?: (e: React.PointerEvent<HTMLButtonElement>) => void;
  isDragSource?: boolean;
  isDropTarget?: boolean;
}

export default function OrderedResourceTicker({
  draggable = false,
  onDragHandlePointerDown,
  isDragSource,
  isDropTarget,
  disabled,
  name,
  ...rest
}: OrderedResourceTickerProps) {
  return (
    <div
      className={[
        "ordered-resource-ticker",
        draggable ? "ordered-resource-ticker--draggable" : "",
        isDragSource ? "ordered-resource-ticker--drag-source" : "",
        isDropTarget ? "ordered-resource-ticker--drop-target" : "",
      ]
        .filter(Boolean)
        .join(" ")}>
      <ResourceTicker name={name} disabled={disabled} onPointerDown={draggable ? onDragHandlePointerDown : undefined} {...rest} />
    </div>
  );
}
