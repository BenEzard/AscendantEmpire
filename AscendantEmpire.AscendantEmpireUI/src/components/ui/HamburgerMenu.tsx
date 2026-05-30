import { useEffect, useRef, useState, type ReactNode } from "react";
import { FaBars } from "react-icons/fa6";
import Tooltip from "./Tooltip";

export interface HamburgerMenuItem {
  key: string;
  icon?: ReactNode;
  label: string;
  tooltip?: string;
  disabled?: boolean;
  onClick: () => void;
}

export interface HamburgerMenuGroup {
  label: string;
  items: HamburgerMenuItem[];
}

interface HamburgerMenuProps {
  groups: HamburgerMenuGroup[];
  /** Accessible label for the trigger button (aria-label). */
  title?: string;
  /** Tooltip shown on hover. Defaults to `title` if not provided. */
  tooltip?: string;
}

export default function HamburgerMenu({ groups, title = "Menu", tooltip }: HamburgerMenuProps) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLSpanElement | null>(null);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (e: PointerEvent) => {
      const el = rootRef.current;
      if (!el) return;
      if (e.target instanceof Node && el.contains(e.target)) return;
      setOpen(false);
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <span ref={rootRef} className="relative inline-flex align-middle">
      <Tooltip content={tooltip ?? title}>
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className={["toolbar-button toolbar-button--secondary", open ? "toolbar-button--active" : ""].filter(Boolean).join(" ")}
          aria-haspopup="menu"
          aria-expanded={open}
          aria-label={title}>
          <FaBars />
        </button>
      </Tooltip>

      {open && (
        <div
          role="menu"
          className="absolute left-0 top-full z-50 mt-1 min-w-48 overflow-hidden rounded-md border border-slate-200 bg-white shadow-sm">
          {groups.map((group) => (
            <div key={group.label} role="group" aria-label={group.label}>
              <div className="border-b border-slate-200 px-3 py-1 text-xs font-semibold text-slate-500">{group.label}</div>
              {group.items.map((item) => {
                const disabled = !!item.disabled;
                return (
                  <Tooltip key={item.key} content={item.tooltip}>
                    <button
                      type="button"
                      role="menuitem"
                      disabled={disabled}
                      onClick={() => {
                        if (disabled) return;
                        item.onClick();
                        setOpen(false);
                      }}
                      className={[
                        "flex w-full items-center gap-2 pl-6 pr-3 py-1.5 text-left text-sm",
                        disabled ? "cursor-not-allowed text-slate-400" : "text-slate-800 hover:bg-slate-100",
                      ]
                        .filter(Boolean)
                        .join(" ")}>
                      {item.icon && <span className="text-base leading-none">{item.icon}</span>}
                      {item.label}
                    </button>
                  </Tooltip>
                );
              })}
            </div>
          ))}
        </div>
      )}
    </span>
  );
}
