namespace AscendantEmpire.Api.Authentication;

/// <summary>
/// Strongly-typed bootstrap configuration, bound from the "Bootstrap" section of app settings.
/// </summary>
public class BootstrapSettings
{
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// The account that should be granted every permission when nobody holds <see cref="Domain.Enums.Authentication.PermissionType.SystemPanel"/> yet.
    /// </summary>
    /// <remarks>
    /// This exists to break a circular dependency: permissions are granted from the System panel, but reaching the System panel requires a permission, so on
    /// a database where nobody has one there would be no way in. Leave it blank to disable the grant entirely — that is the right setting once a real
    /// administrator exists.
    /// </remarks>
    public string AdminUsername { get; init; } = string.Empty;
}
