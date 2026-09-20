using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.BackendContracts.DatabaseSeeding;

/// <summary>
/// Selects which parts of the database a seed job should write.
/// </summary>
/// <remarks>
/// <para>
/// The checkbox list in the Entity Setup Tool maps onto these targets, and the list it draws comes from <c>GET /system/seed/targets</c> rather than being
/// hard-coded, so a new target appears in the dialog without a frontend change.
/// </para>
/// <para>
/// At least one target must be supplied. Prerequisites do not have to be: the server expands the selection transitively before queueing the job, so asking
/// only for the starter templates also seeds the components, tags and tag types they stand on. The dialog ticks them too, but that is a courtesy — this end
/// is where it is made true.
/// </para>
/// </remarks>
public record InitiateDatabaseSeedDto
{
    /// <summary>
    /// The targets to seed. Order is ignored; the server sorts them into dependency order.
    /// </summary>
    public IReadOnlyCollection<SeedTarget> Targets { get; init; } = [];
}
