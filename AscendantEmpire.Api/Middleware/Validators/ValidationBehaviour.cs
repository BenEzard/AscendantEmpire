using AscendantEmpire.Application.Common;

using FluentValidation;

using MediatR;

using System.Reflection;

namespace AscendantEmpire.Api.Middleware.Validators;

public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancel)
    {
        if (validators.Any() == false)
        {
            return await next(cancel);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancel)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
        {
            // Gather error messages
            string[] messages = [.. failures.Select(f => f.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m))];

            // If the expected response type is a ServiceResult<T> (or ServiceResultBase), build a failure response
            // ServiceResult<T> is a nested type inside ServiceResultBase, so use reflection to call its static Fail(...) method.
            if (typeof(TResponse).IsGenericType)
            {
                // Try to find the nested ServiceResult`1 type on ServiceResultBase
                Type? nested = typeof(ServiceResultBase).GetNestedType("ServiceResult`1", BindingFlags.Public | BindingFlags.NonPublic);
                if (nested != null)
                {
                    Type genericArg = typeof(TResponse).GetGenericArguments()[0];
                    Type concreteServiceResult = nested.MakeGenericType(genericArg);

                    MethodInfo? failMethod = concreteServiceResult.GetMethod("Fail", [typeof(IEnumerable<string>)]);
                    if (failMethod != null && failMethod.IsStatic)
                    {
                        object? failResult = failMethod.Invoke(null, [messages]);
                        if (failResult is TResponse typedFail)
                        {
                            return typedFail;
                        }
                    }
                }
            }

            // If the response is exactly ServiceResultBase (non-generic) or some type assignable from it,
            // return a ServiceResultBase.Fail wrapped as object (works when TResponse == ServiceResultBase).
            if (typeof(ServiceResultBase).IsAssignableFrom(typeof(TResponse)))
            {
                var baseFail = ServiceResultBase.Fail(messages);
                return (TResponse)(object)baseFail;
            }

            // Fallback: throw FluentValidation exception so caller/middleware can handle it.
            throw new ValidationException(failures);
        }

        return await next(cancel);
    }
}