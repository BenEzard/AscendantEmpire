namespace AscendantEmpire.FrontendContracts.Structures.GetStructureDisplayInfo;

/// <summary>
/// How one kind of Structure is shown to a player: its name, and the image it is drawn with on the map.
/// </summary>
/// <remarks>
/// Structure kinds are a global catalogue, so this is the same for every Game. The client uses it to put a name into an intelligence sentence - "started construction on a Watchtower" - where the
/// report itself only carries the id.
/// </remarks>
public interface IStructureDisplayInfo
{
    public int StructureId { get; }

    /// <summary>
    /// The kind's stable catalogue code, e.g. <c>village</c>.
    /// </summary>
    /// <remarks>
    /// What the client's artwork is keyed on. The live map read sends a code on each observed Structure, but a last-known-structure marker carries only an id, so this is
    /// how that marker resolves the kind's art.
    /// </remarks>
    public string Code { get; }

    /// <summary>
    /// The kind's name, as shown to the player — "Watchtower", "Village".
    /// </summary>
    public string Name { get; }

    public string MapImagePath { get; }
}
