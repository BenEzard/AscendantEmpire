namespace AscendantEmpire.Domain.Enums.Modifiers;

/// <summary>
/// What kind of thing granted a Player a modifier, so every row it granted can be found and removed together when the Player loses it.
/// </summary>
/// <remarks>
/// Paired with a <c>SourceId</c> naming the particular Technology, Specialist or Department. None of the three is modelled yet, so that id carries no foreign key.
/// </remarks>
public enum ModifierSourceType
{
    Technology = 1,
    Specialist = 2,
    Department = 3
}
