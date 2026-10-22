namespace AscendantEmpire.Api.Options;

/// <summary>
/// Strongly-typed CORS configuration, bound from the "Cors" section of app settings.
/// </summary>
/// <remarks>
/// <para>
/// Which origin the frontend is served from is a property of the deployment rather than of the code, so it lives here instead of as a literal in
/// <c>Program.cs</c>, where every new environment would mean an edit and a redeploy.
/// </para>
/// <para>
/// A list rather than a single value, because more than one front end can legitimately talk to one API: the Vite dev server moves to the next free port
/// when 5173 is already taken, and a hosted environment may serve the same app from both a bare host and a <c>www</c> one.
/// </para>
/// </remarks>
public class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// The browser origins allowed to call this API, each a scheme and host with an optional port and nothing else.
    /// </summary>
    /// <remarks>
    /// Deliberately defaults to empty rather than to a working value. An origin that is right for one environment is wrong in every other, so a fallback
    /// here would quietly become the all-environment answer — the same reasoning that keeps a connection string out of the base <c>appsettings.json</c>.
    /// Startup fails with an explicit message instead; see <see cref="GetValidatedOrigins"/>.
    /// </remarks>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Returns the configured origins, trimmed and checked, ready to hand to <c>WithOrigins</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked at startup rather than trusted, because every way of getting this wrong fails the same silent way. The CORS middleware compares an entry
    /// against the request's <c>Origin</c> header as a plain string, so an empty list, a trailing slash or a stray path simply never matches: the API
    /// answers normally, the browser throws the response away, and the only evidence is a console message on someone else's machine. That is the failure
    /// mode <c>Jwt:SigningKey</c> and the connection string are already checked at startup to avoid.
    /// </para>
    /// <para>
    /// <c>"*"</c> is rejected along with the rest. ASP.NET reads a single-entry <c>"*"</c> list as any-origin, and widening the policy that far should be a
    /// deliberate, reviewable code change rather than something that can arrive from a settings file or an environment variable.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No usable origin is configured, or one of the entries is not a bare scheme/host/port origin.
    /// </exception>
    public string[] GetValidatedOrigins()
    {
        // Blank entries are dropped rather than rejected: an indexed environment variable or a JSON array left with a hole in it is a configuration
        // accident, not an origin someone meant to allow.
        string[] origins = AllowedOrigins
            .Where(origin => string.IsNullOrWhiteSpace(origin) == false)
            .Select(origin => origin.Trim())
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:AllowedOrigins is not configured. Set the browser origins the frontend is served from via the environment's appsettings "
                + $"file, user-secrets, or indexed environment variables ({SectionName}__AllowedOrigins__0, {SectionName}__AllowedOrigins__1, ...).");
        }

        foreach (string origin in origins)
        {
            if (IsBareOrigin(origin) == false)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:AllowedOrigins contains '{origin}', which is not a browser origin. Each entry must be a scheme, a host and an optional "
                    + "port and nothing else — 'http://localhost:5173' or 'https://play.example.com' — with no trailing slash, path, query or wildcard.");
            }
        }

        return origins;
    }

    /// <summary>
    /// Whether the given value is exactly what a browser puts in an <c>Origin</c> header: scheme, host, optional port, nothing more.
    /// </summary>
    private static bool IsBareOrigin(string origin)
    {
        // The trailing slash is the common mistake and has to be caught on the raw string, because Uri normalises it away: "http://host/" and "http://host"
        // both parse to an AbsolutePath of "/". WithOrigins compares the text configuration supplied, so the slash would survive into a comparison that
        // Uri itself no longer sees.
        if (origin.EndsWith('/'))
        {
            return false;
        }

        if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? parsedOrigin) == false)
        {
            return false;
        }

        // A scheme a browser never sends an Origin for, or anything at all past the authority, cannot match a real request.
        return (parsedOrigin.Scheme == Uri.UriSchemeHttp || parsedOrigin.Scheme == Uri.UriSchemeHttps)
            && parsedOrigin.AbsolutePath == "/"
            && string.IsNullOrEmpty(parsedOrigin.Query)
            && string.IsNullOrEmpty(parsedOrigin.Fragment)
            && string.IsNullOrEmpty(parsedOrigin.UserInfo);
    }
}
