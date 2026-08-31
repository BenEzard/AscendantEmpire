import { useMemo } from "react";
import { FaChessKnight, FaRegFolderOpen } from "react-icons/fa6";
import { GoEye, GoEyeClosed } from "react-icons/go";
import { GiRank3, GiWoodStick } from "react-icons/gi";
import { GrCaretNext } from "react-icons/gr";
import { SlClose } from "react-icons/sl";
import { VscNewFile } from "react-icons/vsc";
import type { ToolBarGroup } from "@/components/ui/toolbar/Toolbar";
import { HamburgerMenu } from "@/components/ui";
import { useAccountMenu } from "@/features/auth/hooks/useAccountMenu";
import { CONSTRUCTION_BUTTON_DOM_ID } from "../hooks/useConstructionPanel";

interface UseGameToolbarArgs {
  gameOpen: boolean;
  onNewGame: () => void;
  onOpenGame: () => void;
  onCloseGame: () => void;
  onClearVision: () => void;
  visionActive: boolean;
  onToggleVision: () => void;
  onOpenVisionSettings: () => void;
  onToggleConstruction: () => void;
  unitDesignActive: boolean;
  onToggleUnitDesign: () => void;
  onEndTurn: () => void;
  militaryAdvisorActive: boolean;
  onToggleMilitaryAdvisor: () => void;
}

/** Builds the game screen's toolbar groups (Game menu, Dev Tools, Structures, Advisors). */
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
    onToggleConstruction,
    unitDesignActive,
    onToggleUnitDesign,
    onEndTurn,
    militaryAdvisorActive,
    onToggleMilitaryAdvisor,
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
                      { key: "clear-vision", icon: <GoEyeClosed />, label: "Clear Vision", tooltip: "Clear Vision", disabled: !gameOpen, onClick: onClearVision },
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
        ],
      },
      {
        caption: "Structures",
        buttons: [
          {
            key: "wood-stick",
            icon: <GiWoodStick />,
            tooltip: "Construction Material",
            domId: CONSTRUCTION_BUTTON_DOM_ID,
            onClick: onToggleConstruction,
          },
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
        ],
      },
    ],
    [gameOpen, onNewGame, onOpenGame, onCloseGame, onClearVision, visionActive, onToggleVision, onOpenVisionSettings, onToggleConstruction, unitDesignActive, onToggleUnitDesign, onEndTurn, militaryAdvisorActive, onToggleMilitaryAdvisor, accountHeader, accountGroup],
  );
}
