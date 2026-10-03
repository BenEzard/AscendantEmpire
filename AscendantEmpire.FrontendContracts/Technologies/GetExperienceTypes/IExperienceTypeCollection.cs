namespace AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;

/// <summary>
/// Every kind of experience an action can earn, in display order.
/// </summary>
/// <remarks>
/// Every member of every interface in this family must be declared on the interface, not only on the DT class implementing it - System.Text.Json serialises
/// collection elements by their declared type, so a property missing from the interface is silently dropped.
/// </remarks>
public interface IExperienceTypeCollection
{
    IReadOnlyCollection<IExperienceTypeDetail> ExperienceTypes { get; }
}
