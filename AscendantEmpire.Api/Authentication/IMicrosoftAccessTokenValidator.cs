namespace AscendantEmpire.Api.Authentication;

public interface IMicrosoftAccessTokenValidator
{
    /// <summary>
    /// Validates a Microsoft access token and returns tid/oid.
    /// </summary>
    /// <remarks>Never throws for a bad token.
    /// <list type="bullet">
    /// <item>tid - The tenant the user's account belongs to. Every personal Microsoft account shares the consumer tenant 
    /// 9188040d-6c67-4c5b-b112-36a304b66dad; a work or school account carries its organisation's directory id.</item>
    /// <item>oid - The object id: the user's permanent, unique id within their tenant.</item>
    /// </list>
    /// </remarks>
    /// <returns>Will return "{tid}/{oid}" when the token is valid, null otherwise.</returns>
    Task<string?> ValidateAsync(string accessToken, CancellationToken cancelToken);
}