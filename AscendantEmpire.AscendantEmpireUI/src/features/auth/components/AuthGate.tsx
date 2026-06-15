import { useState, type ReactNode } from "react";
import { clearAuth, getAuth, type AuthState } from "@/api/authToken";
import { Button } from "@/components/ui";
import LoginForm from "./LoginForm";

export interface AuthGateProps {
  children: ReactNode;
}

/**
 * Gates the app behind login. When there's no token it shows the login/register form;
 * otherwise it renders the app plus a small logout control showing who's logged in.
 */
export default function AuthGate({ children }: AuthGateProps) {
  // Seed from storage so a returning user with a valid token skips the login screen.
  const [auth, setAuth] = useState<AuthState | null>(() => getAuth());

  if (!auth) {
    // Re-read storage (authApi just wrote the token) to pick up the new state.
    return <LoginForm onAuthenticated={() => setAuth(getAuth())} />;
  }

  const logout = () => {
    clearAuth();
    // Full reload gives a clean slate: clears the per-page query cache and any in-flight
    // requests that would otherwise fire without a token.
    window.location.reload();
  };

  return (
    <>
      {children}

      {/* Sits above the game toolbar (z-1070) so it stays reachable. */}
      <div className="fixed bottom-2 left-2 z-[1080] flex items-center gap-2 rounded-full border border-slate-200 bg-white/90 px-3 py-1 text-sm shadow-sm backdrop-blur">
        <span className="text-slate-600">
          Signed in as <strong>{auth.username}</strong>
        </span>
        <Button variant="outline-secondary" className="px-2 py-1 text-xs" onClick={logout}>
          Log out
        </Button>
      </div>
    </>
  );
}
