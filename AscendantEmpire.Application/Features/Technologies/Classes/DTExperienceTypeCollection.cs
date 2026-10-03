using AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTExperienceTypeCollection : IExperienceTypeCollection
{
    public required IReadOnlyCollection<IExperienceTypeDetail> ExperienceTypes { get; set; }
}
