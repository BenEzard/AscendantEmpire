namespace AscendantEmpire.FrontendContracts.Units.TrainUnit;

public interface ITrainUnitDto
{
    Guid GameGuid { get; }
    Guid PlayerGuid { get; }
    int XCoord { get; }
    int YCoord { get; }
    int SightRange { get; }
}
