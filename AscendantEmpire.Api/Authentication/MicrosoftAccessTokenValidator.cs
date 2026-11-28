using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using System.Security.Claims;

namespace AscendantEmpire.Api.Authentication;

public sealed class MicrosoftAccessTokenValidator(
    IOptions<MicrosoftSignInSettings> settings,
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
    ILogger<MicrosoftAccessTokenValidator> logger
    ) : IMicrosoftAccessTokenValidator
{
    private readonly MicrosoftSignInSettings _settings = settings.Value;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    /// <inheritdoc />
    public async Task<string?> ValidateAsync(string accessToken, CancellationToken cancelToken) 
    {
        TokenValidationResult validationResult = await ValidateSignatureAndLifetimeAsync(accessToken, cancelToken);

        if (!validationResult.IsValid && validationResult.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            configurationManager.RequestRefresh();
            validationResult = await ValidateSignatureAndLifetimeAsync(accessToken, cancelToken);
        }

        if (!validationResult.IsValid)
        {
            logger.LogInformation("Microsoft access token rejected (Reason: {Reason}).", validationResult.Exception?.GetType().Name);
            return null;
        }

        ClaimsIdentity identity = validationResult.ClaimsIdentity;
        string? authorisedParty = identity.FindFirst("azp")?.Value;
        string? scope = identity.FindFirst("scp")?.Value;
        string? tenantId = identity.FindFirst("tid")?.Value;
        string? objectId = identity.FindFirst("oid")?.Value;

        List<string> errors = [];
        if (authorisedParty is null || !authorisedParty.Equals(_settings.WebClientId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Authorised party is incorrect ('{authorisedParty}')");
        }
        if (scope is null || !scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(_settings.RequiredScope))
        {
            errors.Add($"Scope is incorrect or missing ('{scope}')");
        }
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            errors.Add($"Tenant Id is incorrect or missing ('{tenantId}')");
        }
        if (string.IsNullOrWhiteSpace(objectId))
        {
            errors.Add($"Object Id is incorrect or missing ('{objectId}')");
        }

        if (errors.Count != 0)
        {
            logger.LogInformation("Microsoft access token failed validation (Reason: {Reason}).", string.Join("; ", errors));
            return null;
        }

        return $"{tenantId}/{objectId}";
    }

    private TokenValidationParameters CreateParameters(OpenIdConnectConfiguration configuration) => new()
    {
        ValidAudience = _settings.ApiClientId,
        IssuerSigningKeys = configuration.SigningKeys,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        ValidateIssuer = true,
        // Every tenant signs with its own issuer, so the expected one is built from the token's own tid.
        IssuerValidator = (issuer, token, _) =>
        {
            if (token is JsonWebToken jwt && jwt.TryGetPayloadValue("tid", out string? tenantId) && issuer == $"https://login.microsoftonline.com/{tenantId}/v2.0")
            {
                return issuer;
            }

            throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' does not match the token's tenant.");
        },
    };

    private async Task<TokenValidationResult> ValidateSignatureAndLifetimeAsync(string accessToken, CancellationToken cancelToken)
    {
        OpenIdConnectConfiguration idConfig = await configurationManager.GetConfigurationAsync(cancelToken);

        TokenValidationParameters parameters = CreateParameters(idConfig);

        return await _tokenHandler.ValidateTokenAsync(accessToken, parameters);
    }
}
