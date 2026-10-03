using AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTExperienceTypeDetail : IExperienceTypeDetail
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public required string IconPath { get; set; }

    public required int SortOrder { get; set; }
}
