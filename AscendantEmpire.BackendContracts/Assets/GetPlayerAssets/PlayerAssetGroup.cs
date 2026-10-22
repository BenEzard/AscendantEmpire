namespace AscendantEmpire.BackendContracts.Assets.GetPlayerAssets;

/// <summary>
/// One group of Assets within a grouped category, rendered as a labelled ticker row.
/// </summary>
/// <remarks>
/// <para>
/// Groups carry no unlock rows of their own, on the same terms as <see cref="PlayerAssetCategory"/>: a group is here because at least one Asset
/// in it is available to the Player, and it goes away with the last of them rather than showing an empty row.
/// </para>
/// <para>
/// A group beneath a per-other-player category is a read-time projection rather than a catalogue row, so <see cref="Code"/> rather than
/// <see cref="Id"/> is what identifies one here - see <see cref="SubjectPlayerId"/>.
/// </para>
/// </remarks>
public record PlayerAssetGroup
{
    /// <summary>
    /// The catalogue row's id.
    /// </summary>
    /// <remarks>
    /// <b>Not unique across the returned tree.</b> Every group projected from the same per-other-player template carries that template's id.
    /// </remarks>
    public required int Id { get; init; }

    /// <summary>
    /// The group's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood.Logs".
    /// </summary>
    /// <remarks>
    /// A projected group suffixes the subject Player, e.g. "Society.ForeignPopulation.Subjugated.P12", so it stays unique within the tree.
    /// </remarks>
    public required string Code { get; init; }

    /// <summary>
    /// The group's label: the catalogue's own name, or the subject Player's people name on a per-other-player projection.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The Player whose people this group counts, when it is one of the per-other-player projections; <see langword="null"/> for every ordinary group.
    /// </summary>
    public int? SubjectPlayerId { get; init; }

    /// <summary>
    /// Null where the group carries no explicit order, in which case it sorts after every numbered group and then by name.
    /// </summary>
    public required int? SortOrder { get; init; }

    /// <summary>
    /// The visible Assets in this group, in display order. Never empty - an emptied group is not returned at all.
    /// </summary>
    public required IReadOnlyCollection<PlayerAsset> Assets { get; init; }
}
