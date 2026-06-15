namespace AscendantEmpire.BackendContracts.Authentication;

public record class LoginDto
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
