namespace AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;

/// <summary>
/// One kind of experience an action can earn.
/// </summary>
public interface IExperienceTypeDetail
{
    int Id { get; }

    /// <summary>
    /// The unique code each producing system resolves its own concept against, e.g. "Biome.Jungle".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    string? Description { get; }

    string IconPath { get; }

    int SortOrder { get; }
}
