// Stub for the developer-only "System" tool, created alongside the landing-page split.
// This mirrors the Map Editor as a standalone dev tool (no AuthGate); the real System
// functionality will be filled in later.
export default function System() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-2 bg-slate-900 text-slate-200">
      <h1 className="text-2xl font-semibold">System</h1>
      <p className="text-sm text-slate-400">Developer tool — stub. Functionality coming soon.</p>
    </div>
  );
}
