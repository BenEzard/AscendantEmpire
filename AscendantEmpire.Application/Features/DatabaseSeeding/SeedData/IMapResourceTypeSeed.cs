using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// One MapResourceType the database seed creates, e.g. Timber. Implementations are listed in <see cref="MapResourceTypeSeedData"/>, which is all that adding
/// a resource to the seed takes.
/// </summary>
/// <remarks>
/// <para>
/// A resource owns its own file: its Asset codes, where it grows, and however it holds its Assets - scenarios for a Distribution, layers for a stratified
/// type, a yield for anything else. Nothing here assumes which of those a resource uses.
/// </para>
/// <para>
/// Asset ids are passed in rather than looked up, because seed data can only know an Asset by code: an id is a database identity that differs between
/// databases. <see cref="SeedData.MapResourceTypeSeedData"/> collects every resource's <see cref="AssetCodes"/> so the handler can resolve them all in one
/// query before any resource is built.
/// </para>
/// </remarks>
internal interface IMapResourceTypeSeed
{
    /// <summary>
    /// The type's stable key, which the catalogue upsert matches on. Unique across every seeded resource.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Every <c>assets.Asset</c> code this resource names, in its yields, its scenarios' amounts or its layers.
    /// </summary>
    IReadOnlyCollection<string> AssetCodes { get; }

    /// <summary>
    /// How many rows this resource contributes to the seed step's count: a Distribution's scenarios, a stratified type's layers, or a plain type's yields.
    /// </summary>
    /// <remarks>
    /// Derived from the resource's own data rather than written down, so it cannot fall out of step with what <see cref="Build"/> actually produces.
    /// </remarks>
    int ItemCount { get; }

    /// <summary>
    /// Builds the type as the catalogue contract wants it.
    /// </summary>
    /// <param name="assetIdsByCode">Every code in <see cref="AssetCodes"/>, resolved to its <c>assets.Asset</c> id.</param>
    AddOrUpdateMapResourceTypeDto Build(IReadOnlyDictionary<string, int> assetIdsByCode);
}
