import { useState, type ReactNode } from "react";
import { getAuth, type AuthState } from "@/api/authToken";
import { Button } from "@/components/ui";
import { useAccountMenu } from "../hooks/useAccountMenu";
import LoginForm from "./LoginForm";

export interface AuthGateProps {
  children: ReactNode;
  /**
   * Renders the bottom-left "Signed in as ... / Log out" pill.
   *
   * Off by default: every page whose toolbar has a hamburger menu shows the username and Log out
   * there instead (see useAccountMenu). Match Maker is the one page with no toolbar to host them,
   * so it opts back in.
   */
  showAccountPill?: boolean;
}

/**
 * Gates the app behind login. When there's no token it shows the login/register form;
 * otherwise it renders the app.
 */
export default function AuthGate({ children, showAccountPill = false }: AuthGateProps) {
  // Seed from storage so a returning user with a valid token skips the login screen.
  const [auth, setAuth] = useState<AuthState | null>(() => getAuth());
  // Called unconditionally, before the early return below — hooks can't run conditionally.
  const { logout } = useAccountMenu();

  if (!auth) {
    // Re-read storage (authApi just wrote the token) to pick up the new state.
    return <LoginForm onAuthenticated={() => setAuth(getAuth())} />;
  }

  return (
    <>
      {children}

      {/* Sits above the game toolbar (z-1070) so it stays reachable. */}
      {showAccountPill && (
        <div className="fixed bottom-2 left-2 z-[1080] flex items-center gap-2 rounded-full border border-slate-200 bg-white/90 px-3 py-1 text-sm shadow-sm backdrop-blur">
          <span className="text-slate-600">
            Signed in as <strong>{auth.username}</strong>
          </span>
          <Button variant="outline-secondary" className="px-2 py-1 text-xs" onClick={logout}>
            Log out
          </Button>
        </div>
      )}
    </>
  );
}
