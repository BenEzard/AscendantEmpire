import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
// Global styles, shared by every page entry.
import "./index.css";
import "./styles/bs-vars.css";

// Each page entry creates its own QueryClient. That's intentional: the pages are
// fully separate documents, so there's nothing to share between them and a per-page
// cache keeps the isolation clean.
const queryClient = new QueryClient();

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
