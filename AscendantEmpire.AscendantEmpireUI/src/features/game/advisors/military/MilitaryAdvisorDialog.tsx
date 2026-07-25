import { useRef, useState } from "react";
import { FaSitemap } from "react-icons/fa6";
import { GiRank3 } from "react-icons/gi";

import { Button, PagedDialog } from "@/components/ui";
import MilitaryOrganisationPage from "./MilitaryOrganisationPage";
import type { MilitaryPageHandle } from "./militaryPage";
import OfficerClassPage from "./OfficerClassPage";

interface MilitaryAdvisorDialogProps {
  show: boolean;
  onClose: () => void;
}

/**
 * Military Advisor dialog.
 *
 * <remarks>
 * <para>
 * Opened from the Advisors group of the game toolbar. Carries the Officer Class and Military Organisation
 * pages. A single Save button lives in the dialog footer (rather than on each page) and persists every page's
 * edits together, so a player can change ranks and organisation before saving once.
 * </para>
 * </remarks>
 */
export default function MilitaryAdvisorDialog({ show, onClose }: MilitaryAdvisorDialogProps) {
  // Each page exposes a save() handle; the footer Save calls them all so every page's edits go up together.
  const officerRef = useRef<MilitaryPageHandle>(null);
  const organisationRef = useRef<MilitaryPageHandle>(null);

  const [isSaving, setIsSaving] = useState(false);

  async function handleSaveAll() {
    setIsSaving(true);
    try {
      // allSettled (not all) so both pages attempt their save even if one fails; each page surfaces its own
      // success/error inline.
      await Promise.allSettled([officerRef.current?.save(), organisationRef.current?.save()]);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <PagedDialog
      open={show}
      title="Military Advisor"
      size="lg"
      // Every section supplies an icon, so PagedDialog automatically collapses the nav column to
      // icon width — the labels below move to hover tooltips.
      // Pins the content region so the dialog doesn't resize as the user switches movement domain —
      // domains carry different numbers of organisation groups, and Land's list wraps to two rows.
      height={340}
      panelClassName="thick-border-modal"
      onCancel={onClose}
      onCommit={handleSaveAll}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
          <Button variant="success" disabled={isSaving} onClick={handleSaveAll}>
            {isSaving ? "Saving…" : "Save"}
          </Button>
        </>
      }
      sections={[
        {
          key: "officer-class",
          label: "Officer Class",
          icon: <GiRank3 />,
          content: <OfficerClassPage ref={officerRef} />,
        },
        {
          key: "organisation",
          label: "Military Organisation",
          icon: <FaSitemap />,
          content: <MilitaryOrganisationPage ref={organisationRef} />,
        },
      ]}
    />
  );
}
