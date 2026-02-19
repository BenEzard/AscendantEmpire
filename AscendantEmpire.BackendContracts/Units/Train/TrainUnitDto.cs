using AscendantEmpire.FrontendContracts.Units.TrainUnit;

namespace AscendantEmpire.BackendContracts.Units.Train;

public record class TrainUnitDto : ITrainUnitDto
{
    public Guid GameGuid { get; init; }

    public Guid PlayerGuid { get; init; }

    public int XCoord { get; init; }

    public int YCoord { get; init; }

    public int SightRange { get; init; }
}
