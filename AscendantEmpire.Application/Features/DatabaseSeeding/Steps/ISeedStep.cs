using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Steps;

/// <summary>
/// One independently selectable unit of the database seed — what it is called, what it must follow, and how to run it.
/// </summary>
/// <remarks>
/// <para>
/// One implementation per <see cref="SeedTarget"/>, all registered against this interface. Both the seed executor and the <c>GET /system/seed/targets</c>
/// catalogue enumerate the same registered collection, so the dependency graph the selection dialog draws and the order the seed actually runs in cannot
/// drift apart. Adding a new seed-data class means adding a step here and nothing in the UI.
/// </para>
/// <para>
/// A step runs the work itself rather than handing back an <c>IRequest</c> for the executor to dispatch. That is forced by how handlers are resolved: the
/// handler is looked up against the response type the <i>caller</i> supplies, so a request declaring <c>ServiceResult&lt;T&gt;</c> dispatched through an
/// <c>IRequest&lt;ServiceResultBase&gt;</c> reference would not find its handler — see <see cref="IRequest{TResponse}"/>. 
/// Four of the steps do declare the base response type, but the two unit-design ones return closed generics, so a uniform request-shaped interface would break on exactly those two.
/// </para>
/// </remarks>
internal interface ISeedStep
{
    /// <summary>
    /// The target this step seeds. Unique across every registered step.
    /// </summary>
    SeedTarget Target { get; }

    /// <summary>
    /// The heading this target is listed under in the selection dialog.
    /// </summary>
    SeedTargetGroup Group { get; }

    /// <summary>
    /// The label shown against the checkbox, e.g. "Biomes".
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// One line describing what this target writes, shown under its label so the choice can be made without reading the code.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The targets that must already be seeded — in this run or an earlier one — before this step can succeed.
    /// </summary>
    /// <remarks>
    /// Direct prerequisites only; the resolver walks them transitively, so a step never needs to restate its grandparents. 
    /// An empty collection means the step stands alone.
    /// </remarks>
    IReadOnlyCollection<SeedTarget> DependsOn { get; }

    /// <summary>
    /// Position within the group, ascending. Spaced by 10 so a step can be slotted between two without renumbering.
    /// </summary>
    int SortOrder { get; }

    /// <summary>
    /// How many rows this step would write, for the count shown beside its label.
    /// </summary>
    int ItemCount { get; }

    /// <summary>
    /// Runs the step and returns its fragment of the completion summary, e.g. "12 biome(s)".
    /// </summary>
    /// <remarks>
    /// Throws when the underlying handler fails, so the executor's outer catch records the failure against the Processing row and the steps after it do not
    /// run. Use <see cref="SeedStepDispatch.SendAsync{TResponse}"/> rather than calling the mediator directly, so every step fails the same way.
    /// </remarks>
    /// <param name="mediator">Dispatches the step's upsert request.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>A short phrase naming what was written, for the completion summary.</returns>
    /// <exception cref="InvalidOperationException">The step's handler returned a failure result.</exception>
    Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken);
}
