using AscendantEmpire.FrontendContracts;

using System.Text.Json.Serialization;

namespace AscendantEmpire.Api.Utilities;

public class DTApiErrorResult : IApiErrorResult
{
    public IEnumerable<string> Errors { get; set; }

    /// <summary>
    /// Optional machine-readable detail about the failure, for the cases where the caller has to branch on it rather than just display the message.
    /// </summary>
    /// <remarks>
    /// Omitted from the response entirely when null, so error bodies that carry no detail keep exactly the shape they had before. The shape when it is
    /// present is per-endpoint, and declared by that endpoint's 4xx <c>ProducesResponseType</c>.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Detail { get; set; }

    public DTApiErrorResult(string error) : this([error])
    {

    }

    public DTApiErrorResult(IEnumerable<string> errors)
    {
        Errors = errors;
    }

    public DTApiErrorResult(IEnumerable<string> errors, object? detail) : this(errors)
    {
        Detail = detail;
    }
}
