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
  sections?: readonly { key: K }[];
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
          onSectionChange(sections[index].key);
          return;
        }
        if (e.key === "ArrowUp" || e.key === "ArrowDown") {
          e.preventDefault();
          const currentIndex = sections.findIndex((s) => s.key === activeSectionRef.current);
          const next = e.key === "ArrowDown" ? currentIndex + 1 : currentIndex - 1;
          if (next >= 0 && next < sections.length) {
            onSectionChange(sections[next].key);
          }
        }
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [open, onCommit, onCancel, sections, onSectionChange]);
}
