namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// The whole technology catalogue, in one tree.
/// </summary>
/// <remarks>
/// <para>
/// Every member of every interface in this family must be declared on the interface, not only on the DT class implementing it. The nested collections are
/// typed by their interface, and System.Text.Json serialises collection elements by their declared type - a property missing from an interface is silently
/// dropped from the response.
/// </para>
/// <para>
/// Player-independent while technology customisation is Identical. If a randomised mode is ever implemented, the requirement figures become per-Player and
/// this stops being cacheable - which is why they are grouped on the technology rather than spread across it.
/// </para>
/// </remarks>
public interface ITechnologyCatalogue
{
    IReadOnlyCollection<ICatalogueTechnology> Technologies { get; }
}
