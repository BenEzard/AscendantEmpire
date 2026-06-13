import { useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { updateMapProperties } from "@/features/mapgallery/api/mapGalleryApi";

type Args = {
  onSuccess: (mapName: string, description: string | null) => void;
  onFailureToast: (message: string) => void;
};

/**
 * Submits a name/description update for the open campaign map.
 * On success: invalidates the gallery search cache and calls onSuccess so the
 * parent can update the in-memory openMapIndex.
 */
export function useUpdateMapPropertiesMutation({ onSuccess, onFailureToast }: Args) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      id,
      mapName,
      description,
    }: {
      id: number;
      mapName: string;
      description: string | null;
    }) => updateMapProperties(id, mapName, description),

    onSuccess: (_data, variables) => {
      // Refresh the gallery list so the Open dialog shows the updated name.
      queryClient.invalidateQueries({ queryKey: ["mapGalleryIndexes"] });
      onSuccess(variables.mapName, variables.description);
    },

    onError: (error: unknown) => {
      let message = "Failed to update map properties.";

      if (axios.isAxiosError(error)) {
        const data = error.response?.data;
        if (Array.isArray(data?.errors) && data.errors.length > 0) {
          message = (data.errors as string[]).join(" ");
        } else if (typeof data?.message === "string") {
          message = data.message as string;
        } else if (error.message) {
          message = error.message;
        }
      } else if (error instanceof Error) {
        message = error.message;
      }

      onFailureToast(message);
    },
  });
}
