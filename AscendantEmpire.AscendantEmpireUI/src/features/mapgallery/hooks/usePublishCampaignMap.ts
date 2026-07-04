import { useMutation } from "@tanstack/react-query";
import { postPublishCampaignMap } from "@/features/mapgallery/api/publishCampaignMapApi";

export type PublishCampaignMapVariables = {
  campaignMapGalleryIndexId: number;
  mapName?: string;
};

export function usePublishCampaignMapMutation(opts?: {
  onSuccessToast?: (message: string) => void;
  onFailureToast?: (message: string) => void;
}) {
  return useMutation({
    mutationFn: async (vars: PublishCampaignMapVariables) => {
      await postPublishCampaignMap(vars.campaignMapGalleryIndexId);
    },
    onSuccess: (_data, vars) => {
      const mapLabel = vars.mapName?.trim();
      opts?.onSuccessToast?.(mapLabel ? `Exported "${mapLabel}".` : "Map exported.");
    },
    onError: (err) => {
      const message = err instanceof Error ? err.message : "Failed to export map.";
      opts?.onFailureToast?.(message);
    },
  });
}