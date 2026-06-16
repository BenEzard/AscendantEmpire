import { useState, type FormEvent } from "react";
import { Alert, Button, Spinner } from "@/components/ui";
import { login, register } from "../api/authApi";

type Mode = "login" | "register";

export interface LoginFormProps {
  /** Called after a successful login or registration, with the authenticated username. */
  onAuthenticated: (username: string) => void;
}

/**
 * Centered login / register card shown when the user has no token. Toggles between logging
 * in to an existing account and creating a new one.
 */
export default function LoginForm({ onAuthenticated }: LoginFormProps) {
  const [mode, setMode] = useState<Mode>("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const isRegister = mode === "register";

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const result = isRegister
        ? await register(username.trim(), password, email.trim() || undefined)
        : await login(username.trim(), password);

      onAuthenticated(result.username);
    } catch (err) {
      // The http interceptor already turned server errors into a readable message.
      setError(err instanceof Error ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  const switchMode = () => {
    setMode(isRegister ? "login" : "register");
    setError(null);
  };

  // Explicit bg/text colours: the global :root default text colour is white, so without these
  // the typed value would be white-on-white and invisible in the input.
  const inputClass = "w-full rounded-md border border-slate-300 bg-white px-2 py-2 text-sm text-slate-900";

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <form onSubmit={handleSubmit} className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold text-slate-800">Ascendant Empire</h1>
        <p className="mb-4 text-sm text-slate-500">{isRegister ? "Create an account to play." : "Log in to play."}</p>

        {error && (
          <Alert variant="danger" className="mb-3">
            {error}
          </Alert>
        )}

        <label className="mb-3 block">
          <span className="mb-1 block text-sm font-medium text-slate-700">Username</span>
          <input
            className={inputClass}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </label>

        {isRegister && (
          <label className="mb-3 block">
            <span className="mb-1 block text-sm font-medium text-slate-700">Email (optional)</span>
            <input
              className={inputClass}
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
            />
          </label>
        )}

        <label className="mb-4 block">
          <span className="mb-1 block text-sm font-medium text-slate-700">Password</span>
          <input
            className={inputClass}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={isRegister ? "new-password" : "current-password"}
            required
          />
        </label>

        <Button type="submit" variant="primary" className="w-full" disabled={submitting}>
          {submitting ? <Spinner size="sm" /> : isRegister ? "Create account" : "Log in"}
        </Button>

        <div className="mt-4 text-center text-sm text-slate-500">
          {isRegister ? "Already have an account?" : "Need an account?"}{" "}
          <button type="button" className="font-medium text-[var(--bs-primary)] hover:underline" onClick={switchMode}>
            {isRegister ? "Log in" : "Register"}
          </button>
        </div>
      </form>
    </div>
  );
}
