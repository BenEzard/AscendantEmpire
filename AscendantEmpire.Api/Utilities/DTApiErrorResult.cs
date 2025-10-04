using AscendantEmpire.FrontendContracts;

namespace AscendantEmpire.Api.Utilities;

public class DTApiErrorResult : IApiErrorResult
{
    public IEnumerable<string> Errors { get; set; }

    public DTApiErrorResult(string error) : this(new string[] { error })
    {

    }

    public DTApiErrorResult(IEnumerable<string> errors)
    {
        Errors = errors;
    }
}