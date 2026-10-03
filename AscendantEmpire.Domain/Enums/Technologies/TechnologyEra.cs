namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// The age of the technology tree a Technology sits in.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists for one consumer only: the <c>RandomisedEqual</c> requirement generator</b>, which balances each Player's totals <i>per era</i> so that no
/// Player's randomised weighting ends up lopsided toward one end of the tree. Balancing on the grand total alone would let one Player be cheap in the Stone age
/// and ruinous in the Industrial while another is the reverse, and the two would still net out "equal".
/// </para>
/// <para>
/// It is deliberately <b>not</b> a UI grouping, and nothing outside the generator should read it. There is no category or branch table: the tree's shape comes
/// from <c>TechnologyPrerequisite</c>, and its layout from <c>Technology.TreeColumn</c> and <c>Technology.TreeRow</c>.
/// </para>
/// <para>
/// The ladder below is provisional - the members stand in until the eras are settled. Spaced by 10 so one can be slotted between two others without
/// renumbering the rest.
/// </para>
/// </remarks>
public enum TechnologyEra
{
    Stone = 10,
    Bronze = 20,
    Iron = 30,
    Classical = 40,
    Medieval = 50,
    Renaissance = 60,
    Industrial = 70,
    Modern = 80
}
