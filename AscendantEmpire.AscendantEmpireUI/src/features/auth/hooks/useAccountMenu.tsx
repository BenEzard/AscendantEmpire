import { useCallback, useMemo } from "react";
import { FaArrowRightFromBracket } from "react-icons/fa6";
import { clearAuth, getAuth } from "@/api/authToken";
import type { HamburgerMenuGroup } from "@/components/ui";
import { logout as logoutApi } from "../api/authApi";

/** Stands in for the username in the menu header when there's no auth state to read. */
const LOGGED_OUT_LABEL = "Please log in";

export interface AccountMenu {
  /** Text for the hamburger menu's header row: the logged-in username, or a prompt when logged out. */
  headerLabel: string;
  /**
   * The trailing menu group holding Log out, or null when nobody is logged in. Deliberately
   * unlabelled so HamburgerMenu renders a divider above it rather than a heading.
   */
  group: HamburgerMenuGroup | null;
  /** Ends the session. Exposed separately for the one page that shows a pill instead of a menu. */
  logout: () => Promise<void>;
}

/**
 * Supplies the account header and Log out item shared by every page's hamburger menu, so the
 * logout sequence lives in one place instead of being repeated per toolbar.
 */
export function useAccountMenu(): AccountMenu {
  // Read storage on every render rather than seeding state once the way AuthGate does. The 401
  // interceptor clears auth and then reloads, and the reload isn't instant, so reading live is what
  // lets the header fall back to "Please log in" during that window instead of showing a stale name.
  const auth = getAuth();
  const username = auth?.username ?? null;

  const logout = useCallback(async () => {
    // Best-effort server-side revoke so the refresh token can't be reused; ignore failures
    // (e.g. the access token already expired) — we clear local state regardless.
    try {
      await logoutApi();
    } catch {
      // Intentionally ignored: logout must always clear the client.
    }

    clearAuth();
    // Full reload gives a clean slate: clears the per-page query cache and any in-flight
    // requests that would otherwise fire without a token.
    window.location.reload();
  }, []);

  const group = useMemo<HamburgerMenuGroup | null>(() => {
    // Nothing to log out of, so the menu shows the header prompt on its own.
    if (!username) {
      return null;
    }

    return {
      items: [
        {
          key: "logout",
          icon: <FaArrowRightFromBracket />,
          label: "Log out",
          tooltip: "Log out",
          // The menu item handler is synchronous; the reload inside logout ends the page anyway.
          onClick: () => {
            void logout();
          },
        },
      ],
    };
  }, [username, logout]);

  return { headerLabel: username ?? LOGGED_OUT_LABEL, group, logout };
}
