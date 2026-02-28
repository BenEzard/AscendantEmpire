using AscendantEmpire.Application.Common;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Utilities;

public static class ServiceResultExtensions
{
    /// <summary>
    /// Extension method for results without data.
    /// </summary>
    public static IActionResult ToActionResult(this ServiceResultBase serviceResult)
    {
        IActionResult result;

        switch (serviceResult.StatusCode)
        {
            case ServiceResultStatus.Success:
            case ServiceResultStatus.Created:
            case ServiceResultStatus.SuccessNoContent:
                result = new StatusCodeResult((int)serviceResult.StatusCode);
                break;
            case ServiceResultStatus.PartialSuccess:
                result = new StatusCodeResult((int)serviceResult.StatusCode);
                break;
            case ServiceResultStatus.GenericError:
            case ServiceResultStatus.Forbidden:
            case ServiceResultStatus.NotFound:
            case ServiceResultStatus.Conflict:
            case ServiceResultStatus.Gone:
            case ServiceResultStatus.InternalServerError:
            default:
                IApiErrorResult errorResult = new DTApiErrorResult(serviceResult.Errors);
                result = new ObjectResult(errorResult)
                {
                    StatusCode = (int)serviceResult.StatusCode
                };
                break;
        }

        return result;
    }

    /// <summary>
    /// Extension for results with data, but not provided mapping
    /// </summary>
    public static IActionResult ToActionResult<TDataContract>(this ServiceResult<TDataContract> serviceResult)
        => ToActionResult(serviceResult, (contract) => contract);

    /// <summary>
    /// Extension for results with data, providing a function to map the data contract to a frontend contract
    /// </summary>
    public static IActionResult ToActionResult<TDataContract, TFrontendContract>(this ServiceResult<TDataContract> serviceResult, Func<TDataContract, TFrontendContract> contactMapping)
    {
        IActionResult result;

        switch (serviceResult.StatusCode)
        {
            case ServiceResultStatus.Success:
            case ServiceResultStatus.Created:
            case ServiceResultStatus.SuccessNoContent:
                result = new ObjectResult(contactMapping(serviceResult.SuccessValue))
                {
                    StatusCode = (int)serviceResult.StatusCode
                };
                break;
            case ServiceResultStatus.PartialSuccess:
                result = new ObjectResult(contactMapping(serviceResult.SuccessValue))
                {
                    StatusCode = (int)serviceResult.StatusCode
                };
                break;
            case ServiceResultStatus.GenericError:
            case ServiceResultStatus.Forbidden:
            case ServiceResultStatus.NotFound:
            case ServiceResultStatus.Conflict:
            case ServiceResultStatus.Gone:
            case ServiceResultStatus.InternalServerError:
            default:
                IApiErrorResult errorResult = new DTApiErrorResult(serviceResult.Errors);
                result = new ObjectResult(errorResult)
                {
                    StatusCode = (int)serviceResult.StatusCode
                };
                break;
        }

        return result;
    }
}
