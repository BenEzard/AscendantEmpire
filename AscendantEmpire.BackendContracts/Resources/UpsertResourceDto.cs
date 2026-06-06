namespace AscendantEmpire.BackendContracts.Resources;

public record UpsertResourceDto
{
    public string? Code { get; init; }
    public required string DisplayName { get; init; }
    public string? IconPath { get; init; }
    public bool IsGradedResource { get; init; } = false;
    public bool NeverLuxury { get; init; } = false;

}
