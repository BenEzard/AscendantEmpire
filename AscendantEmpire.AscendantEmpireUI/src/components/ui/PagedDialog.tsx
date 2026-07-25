import { useState, type ReactNode } from "react";
import Modal, { type ModalProps } from "./Modal";
import Tooltip from "./Tooltip";
import { useDialogKeyboardShortcuts } from "@/hooks/useDialogKeyboardShortcuts";

export interface PagedDialogSection<K extends string = string> {
  key: K;
  label: ReactNode;
  content: ReactNode;
  /**
   * Optional icon shown in the nav button in place of the label. When set, the button shows the
   * tab number followed by this icon, and the `label` becomes the button's hover tooltip (a string
   * label is used verbatim; a non-string label yields no tooltip). When *every* section carries an
   * icon the whole nav column collapses to icon width — see `PagedDialogProps.sidebarWidth`.
   */
  icon?: ReactNode;
  /** When true, the section's nav button is shown greyed-out and cannot be selected. */
  disabled?: boolean;
}

export interface PagedDialogProps<K extends string = string> {
  open: boolean;
  title?: ReactNode;
  onCancel: () => void;
  onCommit?: () => void;
  footer?: ReactNode;
  size?: ModalProps["size"];
  /** Explicit panel max width (number = px). Overrides `size`. See Modal's `width`. */
  width?: ModalProps["width"];
  /**
   * Fixed height for the section content region (number = px). Pins the dialog so it never changes
   * height as the user switches pages or edits content — pages taller than this scroll internally.
   * Leave undefined to size to the tallest page (the default behaviour).
   */
  height?: number | string;
  /**
   * Controls the nav (left) column width on `md`+ screens.
   * "ratio" (default) pins it to a third of the dialog; "content" sizes it to the widest nav
   * label, capped at 16rem so a long label can't unbalance the layout. Both modes stack to a
   * single column below `md`.
   *
   * Note: this is overridden automatically when every section carries an `icon` — the column then
   * collapses to the (number + icon) content width regardless of the value set here.
   */
  sidebarWidth?: "ratio" | "content";
  panelClassName?: string;
  sections: readonly PagedDialogSection<K>[];
  header?: ReactNode;
  /** Provide to control the active section externally. */
  activeSection?: K;
  /** Called when the user navigates to a different section. */
  onSectionChange?: (key: K) => void;
}

export default function PagedDialog<K extends string = string>({
  open,
  title,
  onCancel,
  onCommit,
  footer,
  size,
  width,
  height,
  sidebarWidth = "ratio",
  panelClassName,
  sections,
  header,
  activeSection: activeSectionProp,
  onSectionChange,
}: PagedDialogProps<K>) {
  const [internalSection, setInternalSection] = useState<K>(sections[0]?.key as K);

  const activeSection = activeSectionProp ?? internalSection;
  const setActiveSection = (key: K) => {
    setInternalSection(key);
    onSectionChange?.(key);
  };

  useDialogKeyboardShortcuts({
    open,
    onCommit,
    onCancel,
    sections,
    activeSection,
    onSectionChange: setActiveSection,
  });

  // Icon mode kicks in only when every section supplies an icon: the nav column then shows just the
  // tab number + icon (label moves to a tooltip) and shrinks to that content's width. A mix of
  // icon and text sections stays in the normal label layout so the text tabs aren't clipped.
  const iconMode = sections.length > 0 && sections.every((section) => section.icon != null);
  const contentSized = sidebarWidth === "content";

  // Three layouts for the nav (left) column on `md`+ screens:
  //  - iconMode: an auto-sized left track that hugs the (number + icon) content — as narrow as possible.
  //  - "content": an auto-sizing left track capped at 16rem (floored at min-content so a capped label
  //    can still wrap).
  //  - "ratio" (default): the fixed 12-col split (a third / two-thirds).
  // All three collapse to a single column below `md`.
  const gridClassName = iconMode
    ? "grid grid-cols-1 md:grid-cols-[auto_1fr] gap-4"
    : contentSized
      ? "grid grid-cols-1 md:grid-cols-[minmax(min-content,16rem)_1fr] gap-4"
      : "grid grid-cols-12 gap-4";
  // The auto/content tracks size themselves, so their column needs no span class; only the fixed
  // 12-col "ratio" layout pins the nav to md:col-span-4.
  const navColClassName = iconMode || contentSized ? "" : "col-span-12 md:col-span-4";
  const contentColClassName =
    iconMode || contentSized ? "min-h-0 overflow-y-auto" : "col-span-12 md:col-span-8 min-h-0 overflow-y-auto";

  return (
    <Modal open={open} title={title} onClose={onCancel} footer={footer} size={size} width={width} panelClassName={panelClassName}>
      {header}
      {/* When `height` is set, the whole content row is pinned to that height so the dialog never resizes;
          the content column (below) scrolls when a page exceeds it. */}
      <div className={gridClassName} style={height == null ? undefined : { height: typeof height === "number" ? `${height}px` : height }}>
        <div className={navColClassName}>
          {/* flex-col (rather than a plain block) so that when a nav button is wrapped in the
              inline-flex Tooltip below, the wrapper still stretches full width and the items stack —
              matching the un-wrapped block layout exactly when no tooltip is present. */}
          <div className="flex flex-col rounded-md border border-slate-200 bg-white">
            {sections.map(({ key, label, icon, disabled }, index) => {
              // Only a string label can drive the tooltip / accessible name once an icon hides the
              // visible text; a non-string (ReactNode) label yields no tooltip.
              const labelText = typeof label === "string" ? label : undefined;
              // The custom Tooltip only appears when a section is icon-only (Tooltip renders nothing
              // extra when `content` is undefined, so text sections keep the button as a direct child).
              const tooltip = icon != null ? labelText : undefined;
              return (
                <Tooltip key={key} content={tooltip}>
                  <button
                    type="button"
                    disabled={disabled}
                    // With the label hidden behind an icon, keep the tab identifiable to screen readers
                    // (the visual tooltip is the custom bubble from the Tooltip wrapper, not `title`).
                    aria-label={icon != null ? labelText : undefined}
                    onClick={disabled ? undefined : () => setActiveSection(key)}
                    className={[
                      "block w-full px-3 py-2 text-left text-sm",
                      index > 0 ? "border-t border-slate-200" : "",
                      disabled
                        ? "cursor-not-allowed text-slate-300"
                        : activeSection === key
                          ? "nav-item-selected font-semibold"
                          : "hover:bg-slate-50",
                    ]
                      .filter(Boolean)
                      .join(" ")}>
                    {/* The tab number is always shown; when an icon is provided it takes the label's
                        place (label becomes the tooltip), otherwise the label text renders inline. */}
                    <span className="mr-2 text-xs text-slate-300">{index + 1}</span>
                    {/* text-2xl bumps the icon size (react-icons scale with font-size / 1em) well above
                        the button's own text-sm, so the icon reads clearly at the collapsed nav width. */}
                    {icon != null ? <span className="inline-flex align-middle text-2xl">{icon}</span> : label}
                  </button>
                </Tooltip>
              );
            })}
          </div>
        </div>

        {/* min-h-0 lets this grid item shrink below its content so overflow-y-auto can scroll when a
            fixed `height` is in effect; with no height it's a no-op (content sizes naturally). */}
        <div className={contentColClassName}>
          <div className="grid">
            {sections.map(({ key, content }) => (
              <div key={key} style={{ gridArea: "1/1" }} className={activeSection !== key ? "invisible" : ""}>
                {content}
              </div>
            ))}
          </div>
        </div>
      </div>
    </Modal>
  );
}
