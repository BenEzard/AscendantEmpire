using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Classes;

/// <summary>
/// Turns a chosen set of <see cref="SeedTarget"/>s into the ordered list of steps that actually has to run.
/// </summary>
/// <remarks>
/// <para>
/// Two jobs, both of which have to agree: expanding a selection over its prerequisites, and ordering the result so a step never runs before something it depends on. 
/// The selection dialog ticks prerequisites for the user, but this is where it is made true — the client's tick state is a convenience, and a
/// request naming only <c>StarterTemplates</c> still seeds the components, tags and tag types underneath it.
/// </para>
/// <para>
/// Pure: no database, no mediator, nothing but the registered steps and the requested targets. That keeps the graph rules testable on their own.
/// </para>
/// </remarks>
internal static class SeedTargetResolver
{
    /// <summary>
    /// Expands <paramref name="requested"/> over its transitive prerequisites.
    /// </summary>
    /// <remarks>
    /// Used by the initiating handler so the Processing row records what the job will really do, not just what was asked for. 
    /// Order is not meaningful here — use <see cref="Resolve"/> when the steps are about to be run.
    /// </remarks>
    /// <param name="steps">Every registered step.</param>
    /// <param name="requested">The targets the caller picked. May already include some or all prerequisites.</param>
    /// <returns>The closed set of targets, in the same order <see cref="Resolve"/> would run them.</returns>
    /// <exception cref="InvalidOperationException">A requested target has no registered step, or the graph contains a cycle.</exception>
    public static IReadOnlyList<SeedTarget> ExpandTargets(IEnumerable<ISeedStep> steps, IReadOnlyCollection<SeedTarget> requested)
    {
        return [.. Resolve(steps, requested).Select(step => step.Target)];
    }

    /// <summary>
    /// Expands <paramref name="requested"/> over its prerequisites and returns the steps in an order safe to run.
    /// </summary>
    /// <param name="steps">Every registered step.</param>
    /// <param name="requested">The targets the caller picked.</param>
    /// <returns>
    /// The steps to run, each appearing after everything it depends on. Independent steps keep a stable order — by group, then by sort order — so a full selection always runs the same way rather than 
    /// however the container happened to hand the steps over.
    /// </returns>
    /// <exception cref="InvalidOperationException">A requested target has no registered step, or the graph contains a cycle.</exception>
    public static IReadOnlyList<ISeedStep> Resolve(IEnumerable<ISeedStep> steps, IReadOnlyCollection<SeedTarget> requested)
    {
        Dictionary<SeedTarget, ISeedStep> stepsByTarget = BuildIndex(steps);

        List<ISeedStep> ordered = [];
        HashSet<SeedTarget> visited = [];

        // The path currently being walked, so a cycle is caught the moment it closes rather than as a stack overflow.
        HashSet<SeedTarget> inProgress = [];

        // Walk the requested targets in the same stable order the output uses, so two selections containing the same
        // independent steps always emit them the same way round.
        foreach (SeedTarget target in Sorted(requested, stepsByTarget))
        {
            Visit(target, stepsByTarget, visited, inProgress, ordered);
        }

        return ordered;
    }

    /// <summary>
    /// Indexes the registered steps by target, rejecting a target claimed by two steps.
    /// </summary>
    /// <remarks>
    /// A duplicate is a registration mistake rather than anything a caller did, so it throws — the same way the mediator refuses two handlers for one request.
    /// </remarks>
    private static Dictionary<SeedTarget, ISeedStep> BuildIndex(IEnumerable<ISeedStep> steps)
    {
        Dictionary<SeedTarget, ISeedStep> stepsByTarget = [];

        foreach (ISeedStep step in steps)
        {
            if (stepsByTarget.TryAdd(step.Target, step) == false)
            {
                throw new InvalidOperationException($"More than one seed step is registered for target '{step.Target}': {stepsByTarget[step.Target].GetType().Name} and {step.GetType().Name}.");
            }
        }

        return stepsByTarget;
    }

    /// <summary>
    /// Adds <paramref name="target"/> to <paramref name="ordered"/> after everything it depends on, depth-first.
    /// </summary>
    private static void Visit(
        SeedTarget target,
        Dictionary<SeedTarget, ISeedStep> stepsByTarget,
        HashSet<SeedTarget> visited,
        HashSet<SeedTarget> inProgress,
        List<ISeedStep> ordered)
    {
        if (visited.Contains(target))
        {
            return;
        }

        if (inProgress.Add(target) == false)
        {
            throw new InvalidOperationException($"The seed dependency graph contains a cycle involving '{target}'.");
        }

        if (stepsByTarget.TryGetValue(target, out ISeedStep? step) == false)
        {
            throw new InvalidOperationException($"No seed step is registered for target '{target}'.");
        }

        foreach (SeedTarget dependency in Sorted(step.DependsOn, stepsByTarget))
        {
            Visit(dependency, stepsByTarget, visited, inProgress, ordered);
        }

        inProgress.Remove(target);
        visited.Add(target);
        ordered.Add(step);
    }

    /// <summary>
    /// Orders targets by their step's group then sort order, so the walk is deterministic whatever order the container listed the steps in.
    /// </summary>
    /// <remarks>
    /// A target with no registered step sorts last rather than throwing here — <see cref="Visit"/> raises the clearer error once it reaches it.
    /// </remarks>
    private static IEnumerable<SeedTarget> Sorted(IEnumerable<SeedTarget> targets, Dictionary<SeedTarget, ISeedStep> stepsByTarget)
    {
        return targets
            .Distinct()
            .OrderBy(target => stepsByTarget.TryGetValue(target, out ISeedStep? step) ? (int)step.Group : int.MaxValue)
            .ThenBy(target => stepsByTarget.TryGetValue(target, out ISeedStep? step) ? step.SortOrder : int.MaxValue)
            .ThenBy(target => (int)target);
    }
}
