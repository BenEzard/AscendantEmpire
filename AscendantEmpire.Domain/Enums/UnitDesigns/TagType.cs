namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// What a <c>Tag</c> represents, so structural classifications are kept distinct from bonus tags.
/// </summary>
public enum TagType
{
    /// <summary>
    /// The tag is a structural classification of a Component, such as "Engine" or "Hull".
    /// </summary>
    Classification,
    
    /// <summary>
    /// The tag represents a temporary or permanent enhancement or buff to Combat.
    /// </summary>
    Combat,

    /// <summary>
    /// The tag represents a temporary or permanent enhancement or buff to Movement.
    /// </summary>
    Movement,

    /// <summary>
    /// The tag represents a unique capability or function of a unit component.
    /// </summary>
    Ability,
}
