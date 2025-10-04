using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.FrontendContracts;

public interface IApiErrorResult
{
    [Required]
    IEnumerable<string> Errors { get; }
}
