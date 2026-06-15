namespace AscendantEmpire.BackendContracts.Authentication;

public record class RegisterDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    /// <summary>Optional contact email.</summary>
    public string? Email { get; init; }
}
