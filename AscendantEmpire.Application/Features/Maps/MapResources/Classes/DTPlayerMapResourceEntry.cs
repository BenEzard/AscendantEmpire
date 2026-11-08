using AscendantEmpire.FrontendContracts.MapResources.GetPlayerMapResources;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Classes;

public class DTPlayerMapResourceEntry : IPlayerMapResourceEntry
{
    public int X { get; set; }

    public int Y { get; set; }

    public byte Slot { get; set; }

    public int MapResourceTypeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public bool IsObserved { get; set; }

    public long? EstimatedQuantity { get; set; }
}
