using AscendantEmpire.Domain.Enums.Resources;
using AscendantEmpire.FrontendContracts.Resources.GetPlayerResources;

namespace AscendantEmpire.Application.Features.Resources.Classes;

public class DTPlayerResourceCategory : IPlayerResourceCategory
{
    public required int Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public required ResourceCategoryType CategoryType { get; set; }

    public required int SortOrder { get; set; }

    public required IReadOnlyCollection<IPlayerResourceGroup> Groups { get; set; }

    public required IReadOnlyCollection<IPlayerResource> Resources { get; set; }
}
