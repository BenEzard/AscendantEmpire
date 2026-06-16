namespace AscendantEmpire.BackendContracts.Authentication;

public record class RefreshTokenDto
{
    /// <summary>The raw refresh token previously issued to the client.</summary>
    public required string RefreshToken { get; init; }
}
