using AscendantEmpire.FrontendContracts.Resources.GetPlayerResources;

namespace AscendantEmpire.Application.Features.Resources.Classes;

public class DTPlayerResourceMetaCategory : IPlayerResourceMetaCategory
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required string IconPath { get; set; }

    public required int SortOrder { get; set; }

    public required IReadOnlyCollection<IPlayerResourceCategory> Categories { get; set; }
}
