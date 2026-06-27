import { useEffect, useRef } from "react";

const INPUT_TAGS = ["INPUT", "TEXTAREA", "SELECT"];

export function useDialogKeyboardShortcuts<K extends string>({
  open,
  onCommit,
  onCancel,
  sections,
  activeSection,
  onSectionChange,
}: {
  open: boolean;
  onCommit?: () => void;
  onCancel?: () => void;
  sections?: readonly { key: K; disabled?: boolean }[];
  activeSection?: K;
  onSectionChange?: (key: K) => void;
}) {
  // Keep a ref so the keydown handler always sees the latest activeSection
  // without needing to be re-registered every time it changes.
  const activeSectionRef = useRef(activeSection);
  activeSectionRef.current = activeSection;

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (INPUT_TAGS.includes((e.target as HTMLElement).tagName)) return;
      if (e.key === "Escape") {
        onCancel?.();
        return;
      }
      if (e.key === "Enter") {
        onCommit?.();
        return;
      }
      if (sections && onSectionChange) {
        const index = parseInt(e.key, 10) - 1;
        if (index >= 0 && index < sections.length) {
          // Number jump: ignore the key when it targets a disabled section.
          if (!sections[index].disabled) {
            onSectionChange(sections[index].key);
          }
          return;
        }
        if (e.key === "ArrowUp" || e.key === "ArrowDown") {
          e.preventDefault();
          const currentIndex = sections.findIndex((s) => s.key === activeSectionRef.current);
          const step = e.key === "ArrowDown" ? 1 : -1;
          // Walk in the chosen direction to the next enabled section, skipping disabled ones.
          for (let next = currentIndex + step; next >= 0 && next < sections.length; next += step) {
            if (!sections[next].disabled) {
              onSectionChange(sections[next].key);
              break;
            }
          }
        }
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onCommit, onCancel, sections, onSectionChange]);
}
