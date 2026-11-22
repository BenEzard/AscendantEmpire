using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

namespace AscendantEmpire.Application.Features.Technologies.Classes;

public class DTCatalogueSustenanceModifierGrant : ICatalogueSustenanceModifierGrant
{
    public required SustenanceResource Resource { get; set; }

    public BiomeType? BiomeTypeId { get; set; }

    public required int FlatValue { get; set; }

    public required int PercentValue { get; set; }
}
