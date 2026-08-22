namespace AscendantEmpire.BackendContracts.UnitDesigns.CatalogueTransfer;

/// <summary>
/// The <c>templates.json</c> payload inside a catalogue export zip: the catalogue templates and the
/// supersession links between them.
/// </summary>
/// <remarks>
/// This is also the request payload for the template bulk-upsert on import. Everything is keyed by stable
/// <c>Code</c>; the components each template references live in the companion <c>components.json</c>.
/// </remarks>
public record CatalogueTemplatesDocument
{
    /// <summary>
    /// The catalogue templates to upsert.
    /// </summary>
    public required IReadOnlyCollection<CatalogueTemplateDto> Templates { get; init; }

    /// <summary>
    /// Directed template upgrade links to upsert. Authoritative per predecessor, mirroring the component
    /// supersession behaviour; a predecessor not mentioned is left untouched.
    /// </summary>
    public IReadOnlyCollection<CatalogueTemplateSupersessionDto> TemplateSupersessions { get; init; } = [];
}
