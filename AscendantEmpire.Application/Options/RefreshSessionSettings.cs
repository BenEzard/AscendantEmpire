namespace AscendantEmpire.Application.Options;

/// <summary>
/// Configuration for the refresh-session lifecycle: how forgiving reuse detection is, and how long revoked or expired rows are kept.
/// </summary>
/// <remarks>
/// <para>
/// Bound from the <c>RefreshSessionSettings</c> section of appsettings. Session <em>lifetime</em> is not here — the absolute expiry is handed to
/// <c>IRefreshSessionService.IssueAsync</c> by the caller and comes from <c>Jwt:RefreshTokenDays</c>, which lives with the rest of the token
/// configuration in the Api project. This section holds only what the Application layer decides for itself.
/// </para>
/// <para>
/// Every value has a working default, so the section may be omitted entirely.
/// </para>
/// </remarks>
public class RefreshSessionSettings
{
    /// <summary>
    /// How long after a rotation the token it retired is still accepted, in seconds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two clients can legitimately present the same refresh token at once — two browser tabs are two independent single-flight guards — and without
    /// this window that benign race would be read as a stolen token and log the user out. Inside the window the retired token rotates again; outside it,
    /// the whole token family is revoked as a probable compromise.
    /// </para>
    /// <para>
    /// This is a security trade-off in both directions: too short and two-tab users get logged out, too long and it widens the window a thief can
    /// replay in. Set it to <c>0</c> to disable the grace entirely once the frontend's refresh guard is sound enough that the race cannot happen.
    /// </para>
    /// </remarks>
    public int ReuseGraceSeconds { get; set; } = 15;

    /// <summary>
    /// How long a session row is kept past its expiry before the pruning sweep deletes it, in days.
    /// </summary>
    /// <remarks>
    /// Not just housekeeping slack: a session revoked for token reuse is evidence, and deleting it the moment it expires would throw away the only
    /// record that anything happened. A week is long enough to investigate and short enough to bound the table and its unique indexes.
    /// </remarks>
    public int RetentionDays { get; set; } = 7;

    /// <summary>How often the pruning sweep runs, in hours.</summary>
    /// <remarks>Rows accumulate at roughly one per login, so the sweep is cheap and its exact cadence does not matter much.</remarks>
    public int PruneIntervalHours { get; set; } = 6;
}
