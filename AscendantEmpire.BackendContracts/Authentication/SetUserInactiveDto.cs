using AscendantEmpire.Domain.Enums.Authentication;

namespace AscendantEmpire.BackendContracts.Authentication;

public record class SetUserInactiveDto
{
    public int UserId { get; init; }

    public InactiveReasonType InactiveReasonType { get; init; }
}