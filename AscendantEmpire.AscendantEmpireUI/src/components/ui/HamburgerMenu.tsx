import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
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
  /**
   * Heading shown above the group's items. Omit it to render a plain divider instead — used for a
   * trailing group (e.g. Log out) that should be set apart from what precedes it without earning a
   * heading of its own.
   */
  label?: string;
  items: HamburgerMenuItem[];
}

interface HamburgerMenuProps {
  groups: HamburgerMenuGroup[];
  /**
   * Non-interactive text pinned to the top of the open menu, above the first group. Styled like a
   * group heading but deliberately outside every group, so keyboard navigation skips it and screen
   * readers don't announce an empty group.
   */
  header?: ReactNode;
  /** Accessible label for the trigger button (aria-label). */
  title?: string;
  /** Tooltip shown on hover. Defaults to `title` if not provided. */
  tooltip?: string;
}

// Shared by the header and the group headings so the two read as the same kind of row.
const HEADING_CLASSNAME = "border-b border-slate-200 px-3 py-1 text-xs font-semibold text-slate-500";

export default function HamburgerMenu({ groups, header, title = "Menu", tooltip }: HamburgerMenuProps) {
  const [open, setOpen] = useState(false);
  const [focusedIndex, setFocusedIndex] = useState<number>(-1);
  const rootRef = useRef<HTMLSpanElement | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);

  // Flat list of all enabled items for keyboard navigation.
  const navigableItems = useMemo(() => groups.flatMap((g) => g.items).filter((item) => !item.disabled), [groups]);

  // Reset focused index whenever the menu opens/closes.
  useEffect(() => {
    if (!open) {
      setFocusedIndex(-1);
      return;
    }

    const onPointerDown = (e: PointerEvent) => {
      const el = rootRef.current;
      if (!el) {
        return;
      }
      if (e.target instanceof Node && el.contains(e.target)) {
        return;
      }
      setOpen(false);
    };

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
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
          navigableItems[focusedIndex].onClick();
          setOpen(false);
          triggerRef.current?.focus();
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
    if (!open || focusedIndex < 0) {
      return;
    }
    const menu = menuRef.current;
    if (!menu) {
      return;
    }
    const buttons = menu.querySelectorAll<HTMLButtonElement>("button[role='menuitem']:not([disabled])");
    // Map navigableItems index to the DOM button — iterate in order to match.
    const enabledItemKeys = navigableItems.map((i) => i.key);
    const key = enabledItemKeys[focusedIndex];
    const btn = Array.from(buttons).find((b) => b.dataset.key === key);
    btn?.focus();
  }, [open, focusedIndex, navigableItems]);

  return (
    <span ref={rootRef} className="relative inline-flex align-middle">
      <Tooltip content={tooltip ?? title}>
        <button
          ref={triggerRef}
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
          ref={menuRef}
          role="menu"
          className="absolute left-0 top-full z-50 mt-1 min-w-48 overflow-visible rounded-md border border-slate-200 bg-white shadow-sm">
          {header && <div className={HEADING_CLASSNAME}>{header}</div>}

          {/* Groups are static for a given render, so the index is a safe key fallback for an unlabelled group. */}
          {groups.map((group, groupIndex) => (
            <div key={group.label ?? `group-${groupIndex}`} role="group" aria-label={group.label}>
              {/*
                An unlabelled group is set apart by a divider, but not when it's the first one — there the
                header's own bottom border, or the menu's top edge, already separates it and a divider would
                just double the hairline.
              */}
              {group.label && <div className={HEADING_CLASSNAME}>{group.label}</div>}
              {!group.label && groupIndex > 0 && <div className="border-t border-slate-200" />}
              {group.items.map((item) => {
                const disabled = !!item.disabled;
                const navIdx = navigableItems.findIndex((i) => i.key === item.key);
                const isFocused = navIdx !== -1 && navIdx === focusedIndex;
                return (
                  <Tooltip key={item.key} content={item.tooltip} className="w-full">
                    <button
                      type="button"
                      role="menuitem"
                      data-key={item.key}
                      disabled={disabled}
                      tabIndex={isFocused ? 0 : -1}
                      onMouseEnter={() => {
                        if (!disabled) {
                          setFocusedIndex(navIdx);
                        }
                      }}
                      onClick={() => {
                        if (disabled) {
                          return;
                        }
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
