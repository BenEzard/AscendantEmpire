using AscendantEmpire.Api.Authentication;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using System.Security.Cryptography;

namespace AscendantEmpire.Tests.Api;

[TestFixture]
public sealed class MicrosoftAccessTokenValidatorTests
{
    private const string _API_CLIENT_ID = "11111111-1111-1111-1111-111111111111";
    private const string _WEB_CLIENT_ID = "22222222-2222-2222-2222-222222222222";
    private const string _PERSONAL_TENANT_ID = "9188040d-6c67-4c5b-b112-36a304b66dad";
    private const string _WORK_TENANT_ID = "33333333-3333-3333-3333-333333333333";
    private const string _OBJECT_ID = "44444444-4444-4444-4444-444444444444";

    private static readonly RsaSecurityKey _signingKey = CreateKey("key-1");

    #region ValidateAsync
    [Test, Description("A valid personal-account token yields its tid/oid provider key, which proves the default helper token passes every check.")]
    public async Task ShouldReturnProviderKeyForPersonalAccountTokenAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: _signingKey);

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.EqualTo($"{_PERSONAL_TENANT_ID}/{_OBJECT_ID}"));
    }

    [Test, Description("A valid work-account token yields its tid/oid provider key, which proves the default helper token passes every check.")]
    public async Task ShouldReturnProviderKeyForWorkAccountTokenAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: _signingKey, tenantId: _WORK_TENANT_ID);

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.EqualTo($"{_WORK_TENANT_ID}/{_OBJECT_ID}"));
    }

    [Test, Description("Should accept required scope.")]
    public async Task ShouldAcceptRequiredScopeAmongOthersAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: _signingKey, adjust: claims => claims["scp"] = "User.Read access_as_user");

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.EqualTo($"{_PERSONAL_TENANT_ID}/{_OBJECT_ID}"));
    }

    [Test, Description("A token issued from another API is refused, so a token obtained elsewhere can't be replayed against this one.")]
    public async Task ShouldRejectWrongAudienceAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: _signingKey, claims => claims["aud"] = Guid.NewGuid().ToString());

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("A token requested by any app but our Web client is refused, since any tenant's app can get consent for our scope.")]
    public async Task ShouldRejectWrongAuthorisedPartyAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(_signingKey, claims => claims["azp"] = Guid.NewGuid().ToString());

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject a token with an issuer on another tenancy.")]
    public async Task ShouldRejectIssuerFromAnotherTenantAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(_signingKey, issuer: $"https://login.microsoftonline.com/{_WORK_TENANT_ID}/v2.0");

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject a token with the version 1.")]
    public async Task ShouldRejectVersionOneIssuerAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(_signingKey, issuer: $"https://sts.windows.net/{_PERSONAL_TENANT_ID}/");

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject an expired token.")]
    public async Task ShouldRejectExpiredTokenAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(_signingKey, expires: DateTime.UtcNow.AddMinutes(-10));

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject a token missing a scope.")]
    public async Task ShouldRejectMissingScopeAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: _signingKey, claim => claim["scp"] = "does-not-exist");

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject a token missing an ObjectId.")]
    public async Task ShouldRejectMissingObjectIdAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(_signingKey, claims => claims.Remove("oid"));

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }

    [Test, Description("Should reject a token from an unknown key.")]
    public async Task ShouldRejectTokenFromUnknownKeyAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(CreateKey("unknown-key"));

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.Null);
        Assert.That(configurationManager.RefreshCount, Is.EqualTo(1));
    }

    [Test, Description("A token signed with a key published after startup triggers one key refresh and is accepted, " +
        "rather than every sign-in failing until restart.")]
    public async Task ShouldRefreshKeysOnceWhenMicrosoftRotatesThemAsync()
    {
        RsaSecurityKey rotatedKey = CreateKey("key-2");
        FakeConfigurationManager configurationManager = new(_signingKey, rotatedKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);
        string token = CreateToken(signingKey: rotatedKey);

        string? providerKey = await validator.ValidateAsync(token, CancellationToken.None);

        Assert.That(providerKey, Is.EqualTo($"{_PERSONAL_TENANT_ID}/{_OBJECT_ID}"));
        Assert.That(configurationManager.RefreshCount, Is.EqualTo(1));
    }

    [Test, Description("Should reject a malformed token.")]
    public async Task ShouldRejectMalformedTokenAsync()
    {
        FakeConfigurationManager configurationManager = new(_signingKey, _signingKey);
        MicrosoftAccessTokenValidator validator = CreateValidator(configurationManager);

        string? providerKey = await validator.ValidateAsync("not-a-jwt", CancellationToken.None);

        Assert.That(providerKey, Is.Null);
    }
    #endregion

    #region Helpers
    private static RsaSecurityKey CreateKey(string keyId) 
        => new(RSA.Create(2048)) { KeyId = keyId };

    private static string CreateToken(RsaSecurityKey signingKey, 
        Action<Dictionary<string, object>>? adjust = null, 
        DateTime? expires = null, 
        string? issuer = null, 
        string tenantId = _PERSONAL_TENANT_ID)
    {
        Dictionary<string, object> claims = new()
        {
            ["aud"] = _API_CLIENT_ID,
            ["azp"] = _WEB_CLIENT_ID,
            ["scp"] = "access_as_user",
            ["tid"] = tenantId,
            ["oid"] = _OBJECT_ID,
            ["ver"] = "2.0",
        };
        adjust?.Invoke(claims);

        DateTime expiry = expires ?? DateTime.UtcNow.AddMinutes(30);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? $"https://login.microsoftonline.com/{tenantId}/v2.0",
            Claims = claims,
            NotBefore = expiry.AddHours(-1),
            IssuedAt = expiry.AddHours(-1),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <summary>
    /// Serves one set of keys until refreshed, then another, so a test can stand in for Microsoft rotating its keys.
    /// </summary>
    private sealed class FakeConfigurationManager(SecurityKey before, SecurityKey after) : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private bool _refreshed;

        public int RefreshCount { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
        {
            OpenIdConnectConfiguration configuration = new();
            configuration.SigningKeys.Add(_refreshed ? after : before);
            return Task.FromResult(configuration);
        }

        public void RequestRefresh()
        {
            _refreshed = true;
            RefreshCount++;
        }
    }
    private static MicrosoftAccessTokenValidator CreateValidator(FakeConfigurationManager configurationManager)
    {
        IOptions<MicrosoftSignInSettings> settings = Options.Create(new MicrosoftSignInSettings
        {
            ApiClientId = _API_CLIENT_ID,
            WebClientId = _WEB_CLIENT_ID
        });

        return new MicrosoftAccessTokenValidator(settings, configurationManager, NullLogger<MicrosoftAccessTokenValidator>.Instance);
    }
    #endregion Helpers
}
