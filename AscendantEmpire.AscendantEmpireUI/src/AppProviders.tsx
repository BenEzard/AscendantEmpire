import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
// Global styles, shared by every page entry.
import "./index.css";
import "./styles/bs-vars.css";

// Each page entry creates its own QueryClient. That's intentional: the pages are
// fully separate documents, so there's nothing to share between them and a per-page
// cache keeps the isolation clean.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Treat fetched data as fresh for a minute so components mounting/unmounting
      // across the app don't trigger a flurry of redundant refetches.
      staleTime: 60_000,
      // Don't refetch every query just because the window regained focus.
      refetchOnWindowFocus: false,
      // One retry is enough for this API; the default of 3 just delays surfacing errors.
      retry: 1,
    },
  },
});

// Wraps a page's root feature with the providers that used to live in main.tsx.
// Factored out so the game and map-editor entries stay identical and DRY.
export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools />
    </QueryClientProvider>
  );
}
