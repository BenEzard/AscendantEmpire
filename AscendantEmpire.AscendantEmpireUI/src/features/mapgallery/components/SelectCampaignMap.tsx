import { useEffect, useMemo, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Spinner, Tooltip } from "@/components/ui";
import { mapGalleryIndexesKey, searchMapGalleryIndexes, type CampaignMapGalleryIndex } from "@/features/mapgallery/api/mapGalleryApi";
import SelectCampaignMapPreview from "./SelectCampaignMapPreview";

type Props = {
  enabled: boolean;
  filter: string;
  onFilterChange: (value: string) => void;
  selectedMapId: number | null;
  onSelectedMapIdChange: (value: number | null) => void;
  onSelectedMapChange?: (value: CampaignMapGalleryIndex | null) => void;
  onConfirmSelection?: (value: CampaignMapGalleryIndex) => void;
  refreshNonce?: number;
  tableMaxHeightClassName?: string;
};

export default function SelectCampaignMap({
  enabled,
  filter,
  onFilterChange,
  selectedMapId,
  onSelectedMapIdChange,
  onSelectedMapChange,
  onConfirmSelection,
  refreshNonce,
  tableMaxHeightClassName = "max-h-[320px]",
}: Props) {
  const previousRefreshNonce = useRef(refreshNonce);

  const {
    data: indexes,
    isLoading,
    isFetching,
    error,
    refetch,
  } = useQuery<CampaignMapGalleryIndex[], Error>({
    queryKey: mapGalleryIndexesKey,
    queryFn: ({ signal }) => searchMapGalleryIndexes(signal),
    enabled,
    refetchOnWindowFocus: false,
  });

  const filteredMaps = useMemo(() => {
    const list = indexes ?? [];
    const normalizedFilter = filter.trim().toLowerCase();
    if (!normalizedFilter) return list;

    return list.filter((item) => {
      const haystack = `${item.mapName} ${item.description} ${item.width}x${item.height}`.toLowerCase();
      return haystack.includes(normalizedFilter);
    });
  }, [filter, indexes]);

  const selectedMap = useMemo(() => {
    if (selectedMapId == null) return null;
    return (indexes ?? []).find((item) => item.campaignMapGalleryIndexId === selectedMapId) ?? null;
  }, [indexes, selectedMapId]);

  useEffect(() => {
    onSelectedMapChange?.(selectedMap);
  }, [onSelectedMapChange, selectedMap]);

  useEffect(() => {
    if (selectedMapId != null && indexes != null && selectedMap == null) {
      onSelectedMapIdChange(null);
    }
  }, [indexes, onSelectedMapIdChange, selectedMap, selectedMapId]);

  useEffect(() => {
    if (previousRefreshNonce.current === refreshNonce) return;
    previousRefreshNonce.current = refreshNonce;

    if (enabled) {
      refetch();
    }
  }, [enabled, refetch, refreshNonce]);

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(280px,1fr)]">
      <div className="flex min-w-0 flex-col gap-3">
        <div className="flex items-center gap-2">
          <input
            className="w-full rounded-md border border-slate-300 px-2 py-2 text-sm"
            placeholder="Filter maps by name or description..."
            value={filter}
            onChange={(e) => onFilterChange(e.target.value)}
          />
          <Button variant="outline-secondary" onClick={() => refetch()} disabled={isLoading || isFetching}>
            Refresh
          </Button>
        </div>

        {error && <Alert variant="danger">Failed to load maps: {error.message}</Alert>}

        {isLoading ? (
          <div className="flex items-center gap-2 text-slate-600">
            <Spinner size="sm" />
            <span>Loading maps...</span>
          </div>
        ) : (
          <div className={[tableMaxHeightClassName, "overflow-auto rounded-md border border-slate-200"].join(" ")}>
            <table className="w-full text-left text-sm">
              <thead className="sticky top-0 bg-slate-50 text-slate-700">
                <tr>
                  <th className="px-3 py-2" style={{ width: "65%" }}>
                    Name
                  </th>
                  <th className="px-3 py-2" style={{ width: 120 }}>
                    Size
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredMaps.length === 0 ? (
                  <tr>
                    <td colSpan={2} className="px-3 py-3 text-slate-500">
                      No maps found.
                    </td>
                  </tr>
                ) : (
                  filteredMaps.map((item) => {
                    const isSelected = item.campaignMapGalleryIndexId === selectedMapId;

                    return (
                      <tr
                        key={item.campaignMapGalleryIndexId}
                        className={["cursor-pointer border-t border-slate-100 hover:bg-slate-50", isSelected ? "bg-slate-100" : ""].join(" ")}
                        role="button"
                        onClick={() => onSelectedMapIdChange(item.campaignMapGalleryIndexId)}
                        onDoubleClick={() => {
                          onSelectedMapIdChange(item.campaignMapGalleryIndexId);
                          onConfirmSelection?.(item);
                        }}
                        title={onConfirmSelection ? "Double-click to confirm" : undefined}>
                        <td className="px-3 py-2 font-normal text-slate-800">
                          <Tooltip content={item.description} className="max-w-full align-middle">
                            <span className="inline-block max-w-full truncate">{item.mapName}</span>
                          </Tooltip>
                        </td>
                        <td className="px-3 py-2 text-slate-600">
                          {item.width}×{item.height}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <SelectCampaignMapPreview selectedMapId={selectedMapId} selectedMapName={selectedMap?.mapName} />
    </div>
  );
}
