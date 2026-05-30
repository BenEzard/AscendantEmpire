import { useState, type ReactNode } from "react";

interface TooltipProps {
  content: string | undefined;
  children: ReactNode;
}

export default function Tooltip({ content, children }: TooltipProps) {
  const [visible, setVisible] = useState(false);

  if (!content) return <>{children}</>;

  return (
    <span className="relative inline-flex" onMouseEnter={() => setVisible(true)} onMouseLeave={() => setVisible(false)}>
      {children}
      {visible && (
        <span
          role="tooltip"
          className="pointer-events-none absolute bottom-full left-1/2 z-50 mb-1 -translate-x-1/2 whitespace-nowrap rounded bg-black px-2 py-1 text-xs text-white">
          {content}
        </span>
      )}
    </span>
  );
}
