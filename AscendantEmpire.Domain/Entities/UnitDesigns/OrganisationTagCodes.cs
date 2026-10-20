namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// The <see cref="Tag.Code"/> values of the military organisation tags that more than one place has to agree on.
/// </summary>
/// <remarks>
/// <para>
/// An organisation tag differs from a capability tag in what makes the literal dangerous. Nothing gates behaviour on these - a Force takes its organisation
/// from whatever tag is fitted, whichever one that turns out to be. What they do have is a <b>definition in one file and references in others</b>: the tag is
/// authored in <c>TagSeedData</c> and named again by every component that carries it, so renaming the definition alone leaves the component pointing at a tag
/// that is no longer there, and the upsert accepts it.
/// </para>
/// <para>
/// Only codes with that split belong here. The long tail of authored organisation tags lives entirely in the catalogue and needs no constant.
/// </para>
/// <para>
/// A tag listed here is still ordinary catalogue data. It can be absent from a database that has not run the seed's Tags target.
/// </para>
/// </remarks>
public static class OrganisationTagCodes
{
    /// <summary>
    /// The unled civilian organisation a settler party takes, which is what decides how many of them spawn and how they may be split.
    /// </summary>
    /// <remarks>
    /// Carried by the <c>camping-materials</c> equipment component, and the reason fitting camping gear turns a body of people into settlers. Not to be
    /// confused with the <c>SETTLER</c> <i>unit template</i> code in <c>GameSetupService.StarterTemplateCodes</c>, which is a different catalogue entirely
    /// and merely shares the word.
    /// </remarks>
    public const string SETTLER = "settler";
}
