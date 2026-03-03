import type { BrushKind } from "@/features/mapgallery/tools/brush";

function Square({ filled }: { filled: boolean }) {
  return <span className={["inline-block h-1 w-1 border border-black/20", filled ? "bg-black/50" : "bg-transparent"].join(" ")} />;
}

function BrushPreview({ kind, radius }: { kind: BrushKind; radius: number }) {
  const r = radius;
  const n = 1 + r * 2;

  if (kind === "vline") {
    return (
      <span className="grid" style={{ gridTemplateColumns: "repeat(1, 1fr)", gap: 1 }}>
        {Array.from({ length: n }).map((_, i) => (
          <Square key={i} filled />
        ))}
      </span>
    );
  }

  if (kind === "hline") {
    return (
      <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
        {Array.from({ length: n }).map((_, i) => (
          <Square key={i} filled />
        ))}
      </span>
    );
  }

  if (kind === "diagDown" || kind === "diagUp") {
    return (
      <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
        {Array.from({ length: n * n }).map((_, idx) => {
          const x = (idx % n) - r;
          const y = Math.floor(idx / n) - r;
          const filled = kind === "diagDown" ? y === x : y === -x;
          return <Square key={idx} filled={filled} />;
        })}
      </span>
    );
  }

  if (kind === "square") {
    return (
      <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
        {Array.from({ length: n * n }).map((_, i) => (
          <Square key={i} filled />
        ))}
      </span>
    );
  }

  if (kind === "diamond") {
    return (
      <span className="grid" style={{ gridTemplateColumns: `repeat(${n}, 1fr)`, gap: 1 }}>
        {Array.from({ length: n * n }).map((_, idx) => {
          const x = (idx % n) - r;
          const y = Math.floor(idx / n) - r;
          const filled = Math.abs(x) + Math.abs(y) <= r;
          return <Square key={idx} filled={filled} />;
        })}
      </span>
    );
  }

  return null;
}

export default BrushPreview;
