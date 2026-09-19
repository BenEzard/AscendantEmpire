import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteMapResourceCategory,
  deleteMapResourceType,
  getMapResourceCatalogue,
  getResources,
  putMapResourceCatalogue,
  type MapResourceCataloguePayload,
} from "../api/mapResourcesApi";

export const mapResourceCatalogueKey = ["map-resources", "catalogue"] as const;
export const resourcesKey = ["resources"] as const;

/** The MapResource catalogue, grouped by category. Shared by the Type Manager and the editor palette. */
export function useMapResourceCatalogueQuery() {
  return useQuery({
    queryKey: mapResourceCatalogueKey,
    queryFn: getMapResourceCatalogue,
  });
}

/** Every Resource, for the Type Manager's pickers. Rarely changes, so it is not refetched on focus. */
export function useResourcesQuery() {
  return useQuery({
    queryKey: resourcesKey,
    queryFn: getResources,
    staleTime: 5 * 60 * 1000,
  });
}

type MutationCallbacks = {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
};

/** Saves the catalogue, then refetches it so newly created rows carry their ids. */
export function useSaveMapResourceCatalogueMutation(opts?: MutationCallbacks) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: MapResourceCataloguePayload) => putMapResourceCatalogue(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: mapResourceCatalogueKey });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}

/** Deletes one saved type or category, by kind and code. */
export function useDeleteMapResourceMutation(opts?: MutationCallbacks) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ kind, code }: { kind: "type" | "category"; code: string }) =>
      kind === "type" ? deleteMapResourceType(code) : deleteMapResourceCategory(code),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: mapResourceCatalogueKey });
      opts?.onSuccess?.();
    },
    onError: (error) => opts?.onError?.(error),
  });
}
