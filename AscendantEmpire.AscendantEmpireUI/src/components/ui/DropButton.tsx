import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { FaChevronDown } from "react-icons/fa6";
import Tooltip from "./Tooltip";

export type DropButtonItemKey = string | number;

export interface DropButtonItem {
  key: DropButtonItemKey;
  label: ReactNode;
  /** Optional hover tooltip for this menu item. */
  title?: string;
  active?: boolean;
  disabled?: boolean;
  onSelect: () => void;
  /** Optional mouse-wheel handler while hovering this item. */
  onWheel?: (direction: "up" | "down") => void;
}

interface DropButtonProps {
  label: string;
  icon?: ReactNode;
  title?: string;

  /** Optional classes applied to the outermost wrapper. */
  className?: string;

  /** Optional classes applied to the button frame (the rounded border wrapper). */
  frameClassName?: string;

  disabled?: boolean;
  onClick: () => void;

  items: DropButtonItem[];
  menuDisabled?: boolean;

  /** Layout for the dropdown menu. */
  menuLayout?: "list" | "grid";
  /** Only used when menuLayout is "grid". */
  menuGridColumns?: number;
}

export default function DropButton({
  label,
  icon,
  title,
  className = "",
  frameClassName = "",
  disabled = false,
  onClick,
  items,
  menuDisabled = false,
  menuLayout = "list",
  menuGridColumns = 3,
}: DropButtonProps) {
  const [open, setOpen] = useState(false);
  const [focusedIndex, setFocusedIndex] = useState<number>(-1);
  const rootRef = useRef<HTMLSpanElement | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);
  const arrowButtonRef = useRef<HTMLButtonElement | null>(null);

  const effectiveTitle = title ?? label;

  const hasEnabledItems = useMemo(() => items.some((i) => !i.disabled), [items]);
  const arrowDisabled = disabled || menuDisabled || !hasEnabledItems;

  const navigableItems = useMemo(() => items.filter((i) => !i.disabled), [items]);

  useEffect(() => {
    if (!open) {
      setFocusedIndex(-1);
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      const el = rootRef.current;
      if (!el) return;
      if (e.target instanceof Node && el.contains(e.target)) return;
      setOpen(false);
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        setOpen(false);
        arrowButtonRef.current?.focus();
        return;
      }

      if (e.key === "ArrowDown") {
        e.preventDefault();
        setFocusedIndex((prev) => (prev + 1) % navigableItems.length);
        return;
      }

      if (e.key === "ArrowUp") {
        e.preventDefault();
        setFocusedIndex((prev) => (prev <= 0 ? navigableItems.length - 1 : prev - 1));
        return;
      }

      if (e.key === "Enter" || e.key === " ") {
        if (focusedIndex >= 0 && focusedIndex < navigableItems.length) {
          e.preventDefault();
          navigableItems[focusedIndex].onSelect();
          setOpen(false);
          arrowButtonRef.current?.focus();
        }
        return;
      }

      if (e.key === "Tab") {
        setOpen(false);
      }
    };

    document.addEventListener("pointerdown", onPointerDown, { capture: true });
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown, { capture: true } as AddEventListenerOptions);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open, focusedIndex, navigableItems]);

  // Sync DOM focus to the focused item button.
  useEffect(() => {
    if (!open || focusedIndex < 0) return;
    const menu = menuRef.current;
    if (!menu) return;
    const buttons = menu.querySelectorAll<HTMLButtonElement>("button[role='menuitem']:not([disabled])");
    buttons[focusedIndex]?.focus();
  }, [open, focusedIndex]);

  return (
    <Tooltip content={effectiveTitle}>
      <span ref={rootRef} className={"relative inline-flex align-middle " + className}>
        <span className={"inline-flex overflow-hidden rounded-md border border-black/20 " + frameClassName}>
          <button
            type="button"
            disabled={disabled}
            onClick={disabled ? undefined : onClick}
            className={
              "inline-flex items-center justify-center gap-2 bg-white/90 px-2 py-1.5 text-[1.5rem] leading-none text-black/80 " +
              "hover:bg-white/95 disabled:cursor-not-allowed disabled:text-black/40"
            }
            aria-label={label}>
            {icon}
          </button>

          <button
            ref={arrowButtonRef}
            type="button"
            disabled={arrowDisabled}
            onClick={arrowDisabled ? undefined : () => setOpen((v) => !v)}
            className={
              "inline-flex w-5 items-center justify-center border-l border-black/20 bg-white/90 px-0 py-1.5 text-black/70 " +
              "hover:bg-white/95 disabled:cursor-not-allowed disabled:text-black/40"
            }
            aria-haspopup="menu"
            aria-expanded={open}
            aria-label={label + " menu"}>
            <FaChevronDown aria-hidden="true" size={9} />
          </button>
        </span>

        {open && (
          <div
            ref={menuRef}
            role="menu"
            className="absolute left-0 top-full z-50 mt-1 min-w-48 overflow-hidden rounded-md border border-slate-200 bg-white shadow-sm">
            {menuLayout === "grid" ? (
              <div
                className="grid gap-1 p-1"
                style={{
                  gridTemplateColumns: `repeat(${Math.max(1, menuGridColumns)}, minmax(0, 1fr))`,
                }}>
                {items.map((item) => {
                  const itemDisabled = !!item.disabled;
                  const itemActive = !!item.active;
                  const navIdx = navigableItems.findIndex((i) => i.key === item.key);
                  const isFocused = navIdx !== -1 && navIdx === focusedIndex;

                  return (
                    <Tooltip key={String(item.key)} content={item.title}>
                      <button
                        type="button"
                        role="menuitem"
                        disabled={itemDisabled}
                        tabIndex={isFocused ? 0 : -1}
                        onMouseEnter={() => {
                          if (!itemDisabled) setFocusedIndex(navIdx);
                        }}
                        onWheel={(e) => {
                          if (itemDisabled) return;
                          if (!item.onWheel) return;
                          e.preventDefault();
                          e.stopPropagation();
                          item.onWheel(e.deltaY < 0 ? "up" : "down");
                        }}
                        onClick={() => {
                          if (itemDisabled) return;
                          item.onSelect();
                          setOpen(false);
                        }}
                        className={[
                          "flex items-center justify-center overflow-hidden rounded px-2 py-2",
                          itemDisabled ? "cursor-not-allowed text-slate-400" : "text-slate-800 hover:bg-slate-100",
                          itemActive ? "bg-slate-100" : "",
                        ]
                          .filter(Boolean)
                          .join(" ")}>
                        {item.label}
                      </button>
                    </Tooltip>
                  );
                })}
              </div>
            ) : (
              items.map((item) => {
                const itemDisabled = !!item.disabled;
                const itemActive = !!item.active;
                const navIdx = navigableItems.findIndex((i) => i.key === item.key);
                const isFocused = navIdx !== -1 && navIdx === focusedIndex;

                return (
                  <Tooltip key={String(item.key)} content={item.title}>
                    <button
                      type="button"
                      role="menuitem"
                      disabled={itemDisabled}
                      tabIndex={isFocused ? 0 : -1}
                      onMouseEnter={() => {
                        if (!itemDisabled) setFocusedIndex(navIdx);
                      }}
                      onWheel={(e) => {
                        if (itemDisabled) return;
                        if (!item.onWheel) return;
                        e.preventDefault();
                        e.stopPropagation();
                        item.onWheel(e.deltaY < 0 ? "up" : "down");
                      }}
                      onClick={() => {
                        if (itemDisabled) return;
                        item.onSelect();
                        setOpen(false);
                      }}
                      className={[
                        "block w-full px-3 py-0.5 text-left text-sm",
                        itemDisabled ? "cursor-not-allowed text-slate-400" : "text-slate-800 hover:bg-slate-100",
                        itemActive ? "bg-slate-100 font-semibold" : "",
                      ]
                        .filter(Boolean)
                        .join(" ")}>
                      {item.label}
                    </button>
                  </Tooltip>
                );
              })
            )}
          </div>
        )}
      </span>
    </Tooltip>
  );
}
