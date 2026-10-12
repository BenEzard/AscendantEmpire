import { useMemo } from "react";
import { FaChessKnight, FaPersonWalkingArrowRight, FaRegFolderOpen } from "react-icons/fa6";
import { GoEye, GoEyeClosed } from "react-icons/go";
import { GiMicroscope, GiRank3 } from "react-icons/gi";
import { GrCaretNext, GrPowerCycle } from "react-icons/gr";
import { SlClose } from "react-icons/sl";
import { VscNewFile } from "react-icons/vsc";
import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { HamburgerMenu } from "@/components/ui";
import { useAccountMenu } from "@/features/auth/hooks/useAccountMenu";
import type { PlayerAssetMetaCategory } from "@/features/game/api/playerAssetsApi";
import { buildAssetToolbarButtons } from "./assetToolbarButtons";

interface UseGameToolbarArgs {
  gameOpen: boolean;
  onNewGame: () => void;
  onOpenGame: () => void;
  onCloseGame: () => void;
  onClearVision: () => void;
  visionActive: boolean;
  onToggleVision: () => void;
  onOpenVisionSettings: () => void;
  /** The meta-categories the Player has unlocked, one toolbar button each, in the order the API returned them. */
  assetMetaCategories: PlayerAssetMetaCategory[];
  /** Code of the meta-category whose panel is open, so its button shows as pressed. */
  openAssetMetaCategoryCode: string | null;
  onToggleAssetPanel: (metaCategoryCode: string) => void;
  unitDesignActive: boolean;
  onToggleUnitDesign: () => void;
  onEndTurn: () => void;
  militaryAdvisorActive: boolean;
  onToggleMilitaryAdvisor: () => void;
  scienceAdvisorActive: boolean;
  onToggleScienceAdvisor: () => void;
  /** Remove in F-18. Ends this player's turn and continues as whoever plays next. */
  onEndTurnAndSwitchPlayer: () => void;
  /** Remove in F-18. Ends every turn until it is this player's turn again, and continues as them. */
  onEndRoundAndReturnToPlayer: () => void;
  /** Remove in F-18. True while a developer switch is in flight, so neither button can be pressed twice. */
  devSwitchInProgress: boolean;
}

/**
 * Remove in F-18. Whether the temporary developer hot-seat buttons are offered at all.
 *
 * They call an endpoint that only exists when the API runs in Development, so showing them in a production build would offer two buttons that could do
 * nothing but 404.
 */
const SHOW_DEV_HOT_SEAT_BUTTONS = import.meta.env.DEV;

/** Builds the game screen's toolbar groups (Game menu, Dev Tools, Assets, Structures, Advisors). */
export function useGameToolbar(args: UseGameToolbarArgs): ToolBarGroup[] {
  const {
    gameOpen,
    onNewGame,
    onOpenGame,
    onCloseGame,
    onClearVision,
    visionActive,
    onToggleVision,
    onOpenVisionSettings,
    assetMetaCategories,
    openAssetMetaCategoryCode,
    onToggleAssetPanel,
    unitDesignActive,
    onToggleUnitDesign,
    onEndTurn,
    militaryAdvisorActive,
    onToggleMilitaryAdvisor,
    scienceAdvisorActive,
    onToggleScienceAdvisor,
    onEndTurnAndSwitchPlayer,
    onEndRoundAndReturnToPlayer,
    devSwitchInProgress,
  } = args;

  // The signed-in username and the Log out item, shared with every other page's hamburger menu.
  const { headerLabel: accountHeader, group: accountGroup } = useAccountMenu();

  return useMemo<ToolBarGroup[]>(
    () => [
      {
        caption: "Game",
        buttons: [
          {
            key: "game-menu",
            render: () => (
              <HamburgerMenu
                title="Game menu"
                tooltip="Game"
                header={accountHeader}
                groups={[
                  {
                    label: "Game",
                    items: [
                      { key: "new", icon: <VscNewFile />, label: "New", tooltip: "New Game", onClick: onNewGame },
                      { key: "open", icon: <FaRegFolderOpen />, label: "Open", tooltip: "Open Game", onClick: onOpenGame },
                      { key: "close", icon: <SlClose />, label: "Close", tooltip: "Close Game", disabled: !gameOpen, onClick: onCloseGame },
                    ],
                  },
                  {
                    label: "Dev Tools",
                    items: [
                      {
                        key: "clear-vision",
                        icon: <GoEyeClosed />,
                        label: "Clear Vision",
                        tooltip: "Clear Vision",
                        disabled: !gameOpen,
                        onClick: onClearVision,
                      },
                    ],
                  },
                  // Log out last, separated from the page's own groups. Absent when logged out.
                  ...(accountGroup ? [accountGroup] : []),
                ]}
              />
            ),
          },
        ],
      },
      {
        caption: "Dev Tools",
        buttons: [
          {
            key: "vision",
            icon: <GoEye />,
            tooltip: "Check Vision",
            disabled: !gameOpen,
            active: visionActive,
            onClick: onToggleVision,
            onMiddleClick: onOpenVisionSettings,
            middleClickHint: "settings",
          },
          // Remove in F-18. Both hot-seat buttons, and the spread that conditionally includes them, go with the endpoint they call.
          ...(SHOW_DEV_HOT_SEAT_BUTTONS
            ? [
                {
                  key: "end-turn-with-logout",
                  icon: <FaPersonWalkingArrowRight />,
                  tooltip: "End turn with logout",
                  // No turn to end without a game open, and the request would be refused for want of a game token.
                  disabled: !gameOpen || devSwitchInProgress,
                  onClick: onEndTurnAndSwitchPlayer,
                },
                {
                  key: "end-round-back-to-player",
                  icon: <GrPowerCycle />,
                  tooltip: "End round, back to this player",
                  disabled: !gameOpen || devSwitchInProgress,
                  onClick: onEndRoundAndReturnToPlayer,
                },
              ]
            : []),
        ],
      },
      {
        // Assets — the raw materials a player draws on. One button per meta-category the player has unlocked, each
        // opening its own panel; the group is empty while the tree loads, and for a player with none unlocked.
        caption: "Resources",
        buttons: buildAssetToolbarButtons(assetMetaCategories, openAssetMetaCategoryCode, onToggleAssetPanel),
      },
      {
        caption: "Structures",
        buttons: [
          {
            key: "unit-design",
            icon: <FaChessKnight />,
            tooltip: "Unit Design",
            active: unitDesignActive,
            onClick: onToggleUnitDesign,
          },
          {
            key: "end-turn",
            icon: <GrCaretNext />,
            tooltip: "End Turn",
            // There is no turn to end without a game open, and the request would be refused for want of a game token.
            disabled: !gameOpen,
            onClick: onEndTurn,
          },
        ],
      },
      {
        // Departments/Advisors — see docs/design/departments-advisors.md. Military is the first of
        // several advisors expected to live in this group.
        caption: "Advisors",
        buttons: [
          {
            key: "military-advisor",
            icon: <GiRank3 />,
            tooltip: "Military Advisor",
            disabled: !gameOpen,
            active: militaryAdvisorActive,
            onClick: onToggleMilitaryAdvisor,
          },
          {
            key: "science-advisor",
            icon: <GiMicroscope />,
            tooltip: "Science Advisor",
            disabled: !gameOpen,
            active: scienceAdvisorActive,
            onClick: onToggleScienceAdvisor,
          },
        ],
      },
    ],
    // onEndTurnAndSwitchPlayer, onEndRoundAndReturnToPlayer and devSwitchInProgress: remove in F-18.
    [
      gameOpen,
      onNewGame,
      onOpenGame,
      onCloseGame,
      onClearVision,
      visionActive,
      onToggleVision,
      onOpenVisionSettings,
      // The tree and the toggle are both identity-stable (a frozen empty array or react-query's shared data, and an empty-dep callback), so only the
      // open code moves the groups array — once per open or close, which is what lets the pressed button re-render.
      assetMetaCategories,
      openAssetMetaCategoryCode,
      onToggleAssetPanel,
      unitDesignActive,
      onToggleUnitDesign,
      onEndTurn,
      militaryAdvisorActive,
      onToggleMilitaryAdvisor,
      scienceAdvisorActive,
      onToggleScienceAdvisor,
      onEndTurnAndSwitchPlayer,
      onEndRoundAndReturnToPlayer,
      devSwitchInProgress,
      accountHeader,
      accountGroup,
    ],
  );
}
