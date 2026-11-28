namespace AscendantEmpire.Api.Authentication;

public class MicrosoftSignInSettings
{
    public const string SECTION_NAME = "MicrosoftSignIn";
    public string ApiClientId { get; init; } = string.Empty;
    public string WebClientId { get; init; } = string.Empty;
    public string RequiredScope { get; init; } = "access_as_user";

    /// <summary>
    /// True when both ids are non-blank.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiClientId) && !string.IsNullOrWhiteSpace(WebClientId);
}
