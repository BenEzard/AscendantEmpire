namespace AscendantEmpire.Domain.Enums.MapResources;

/// <summary>
/// What a <c>PlayerMapResourceSwitch</c> turns on or off for one Player.
/// </summary>
/// <remarks>
/// Each kind has a default that applies while a Player holds no switch for it. See <c>PlayerMapResourceSwitch</c> for how switches that disagree are resolved.
/// </remarks>
public enum MapResourceSwitchKind
{
    /// <summary>
    /// Whether the Player can discover instances of the type at all. While off, their discovery likelihood for it is 0, however their units search, but instances they
    /// have already discovered stay discovered. Defaults to the type's <c>IsDiscoverableByDefault</c>.
    /// </summary>
    Discovery = 1,

    /// <summary>
    /// Whether extracting the type gives the Player one particular Resource from its yield, e.g. a Deer's pelt. The quantity on the tile still goes down by the full amount
    /// extracted, so a Resource switched off is lost. Defaults to the yield's <c>IsEnabledByDefault</c>. Never applies to a Distribution, which always gives all of its
    /// Resources.
    /// </summary>
    YieldResource = 2,

    /// <summary>
    /// Whether the Player can survey one layer of a stratified type. Layer 1 is always surveyable; deeper layers default to off, standing in for the tech requirement until
    /// Technology is modelled.
    /// </summary>
    SurveyLayer = 3
}
