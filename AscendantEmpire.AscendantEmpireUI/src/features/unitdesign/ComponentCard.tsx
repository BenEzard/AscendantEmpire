export interface ComponentCardProps {
  name: string;
  properties: Record<string, number>;
  imageUrl?: string;
  imageAlt?: string;
  selected?: boolean;
  onClick?: () => void;
  onDoubleClick?: () => void;
}

export default function ComponentCard({ name, imageUrl, imageAlt, selected = false, onClick, onDoubleClick }: ComponentCardProps) {
  return (
    <button
      type="button"
      className={[
        "h-32 w-full rounded-md px-3 py-3 text-left text-sm box-border",
        "flex flex-col items-start gap-2",
        "hover:bg-slate-50",
        selected ? "border-4 border-[var(--bs-danger)] bg-slate-100" : "border border-slate-200 bg-slate-50",
      ].join(" ")}
      onClick={onClick}
      onDoubleClick={onDoubleClick}
      aria-label={name}>
      <div className="font-medium text-slate-900">{name}</div>
      {imageUrl && <img src={imageUrl} alt={imageAlt ?? name} className="h-16 w-auto object-contain" draggable={false} />}
    </button>
  );
}
