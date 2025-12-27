namespace AscendantEmpire.Application.Common;

public enum ServiceResultStatus
{
    Success = 200,
    Created = 201,
    PartialSuccess = 202, // This is not a standard HTTP status code
    SuccessNoContent = 204,
    GenericError = 400,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    Gone = 410,
    InternalServerError = 500
};

public record ServiceResultBase
{
    /// <summary>
    /// Result status
    /// </summary>
    public ServiceResultStatus StatusCode { get; init; } = ServiceResultStatus.GenericError;

    /// <summary>
    /// If the status code indicates that the request succeeded
    /// </summary>
    public bool HasSucceeded => StatusCode is ServiceResultStatus.Success or ServiceResultStatus.Created or ServiceResultStatus.SuccessNoContent;

    /// <summary>
    /// Return error messages (suitable for end user)
    /// </summary>
    public IReadOnlyCollection<string> Errors { get; init; } = [];

    public static ServiceResultBase Succeed() => new() { StatusCode = ServiceResultStatus.Success };
    public static ServiceResultBase Created() => new() { StatusCode = ServiceResultStatus.Created };
    public static ServiceResultBase SuccessNoContent() => new() { StatusCode = ServiceResultStatus.SuccessNoContent };
    public static ServiceResultBase Fail(string error) => Fail([error]);
    public static ServiceResultBase InternalServerError(string error) => InternalServerError([error]);
    public static ServiceResultBase NotFound(string error) => NotFound([error]);

    /// <summary>
    /// Generic error. Usually indicates that something to do with the request data might be wrong.
    /// </summary>
    public static ServiceResultBase Fail(IEnumerable<string> errors) => new()
    {
        StatusCode = ServiceResultStatus.GenericError,
        Errors = [.. errors]
    };

    public static ServiceResultBase NotFound(IEnumerable<string> errors) => new()
    {
        StatusCode = ServiceResultStatus.NotFound,
        Errors = [.. errors]
    };

    public static ServiceResultBase InternalServerError(IEnumerable<string> errors) => new()
    {
        StatusCode = ServiceResultStatus.InternalServerError,
        Errors = [.. errors]
    };

    /// <summary>
    /// A Service Result with a success object.
    /// </summary>
    public record ServiceResult<TSuccess> : Common.ServiceResultBase
    {
        public TSuccess? SuccessValue { get; init; }

        /// <summary>
        /// 204 OK response, doesn't contain data. Indicates that the request succeeded.
        /// </summary>
        public static ServiceResult<TSuccess> Succeed(TSuccess value) => new()
        {
            StatusCode = ServiceResultStatus.Success,
            SuccessValue = value
        };

        /// <summary>
        /// 201 Created response, doesn't contain data. Indicates that the request succeeded. And a resource was created.
        /// </summary>
        public static ServiceResult<TSuccess> Created(TSuccess value) => new()
        {
            StatusCode = ServiceResultStatus.Created,
            SuccessValue = value
        };

        /// <summary>
        /// 204 No Content response. Indicates that the request succeeded, but there is no content to return.
        /// </summary>
        public static new ServiceResult<TSuccess> SuccessNoContent() => new()
        {
            StatusCode = ServiceResultStatus.SuccessNoContent
        };

        /// <summary>
        /// Generic error. Usually indicates that something to do with the request data might be wrong.
        /// </summary>
        public static new ServiceResult<TSuccess> Fail(string error) => Fail([error]);

        /// <summary>
        /// Generic error. Usually indicates that something to do with the request data might be wrong.
        /// </summary>
        public static new ServiceResult<TSuccess> Fail(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.GenericError,
            Errors = [.. errors]
        };

        /// <summary>
        /// User permissions error. The authenticated user is not allowed to access/modify the resource.
        /// </summary>
        public static ServiceResult<TSuccess> Forbidden(string error) => Forbidden([error]);

        /// <summary>
        /// User permissions error. The authenticated user is not allowed to access/modify the resource.
        /// </summary>
        public static ServiceResult<TSuccess> Forbidden(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Forbidden,
            Errors = [.. errors]
        };

        /// <summary>
        /// Not found error. A resource needed to complete the request could not be found.
        /// </summary>
        public static ServiceResult<TSuccess> NotFound(string error) => NotFound([error]);

        /// <summary>
        /// Not found error. A resource needed to complete the request could not be found.
        /// </summary>
        public static ServiceResult<TSuccess> NotFound(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.NotFound,
            Errors = [.. errors]
        };

        /// <summary>
        /// Conflict error. Indicates that the failed as it conflicted with the current state of a resource (concurrency exception maybe).
        /// </summary>
        public static ServiceResult<TSuccess> Conflict(string error) => Conflict([error]);

        /// <summary>
        /// Conflict error. Indicates that the failed as it conflicted with the current state of a resource (concurrency exception maybe).
        /// </summary>
        public static ServiceResult<TSuccess> Conflict(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Conflict,
            Errors = [.. errors]
        };

        /// <summary>
        /// Gone error. A resource needed to complete this request was initially available, but is no longer available or removed.
        /// </summary>        
        public static ServiceResult<TSuccess> Gone(string error) => Gone([error]);

        /// <summary>
        /// Gone error. A resource needed to complete this request was initially available, but is no longer available or removed.
        /// </summary>
        public static ServiceResult<TSuccess> Gone(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Gone,
            Errors = [.. errors]
        };

        /// <summary>
        /// Internal server error. Indicates that something has gone wrong on the backend, such as an exception being thrown.
        /// </summary>
        public static new ServiceResult<TSuccess> InternalServerError(string error) => InternalServerError([error]);

        /// <summary>
        /// Internal server error. Indicates that something has gone wrong on the backend, such as an exception being thrown.
        /// </summary>
        public static new ServiceResult<TSuccess> InternalServerError(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.InternalServerError,
            Errors = [.. errors]
        };

        /// <summary>
        /// Internal server error. Indicates that something has gone wrong on the backend, such as an exception being thrown.
        /// </summary>
        public static ServiceResult<TSuccess> InternalServerError(Exception ex, string context)
        {
            var list = new List<string> { context, ex.Message };
            if (ex.InnerException != null)
                list.Add(ex.InnerException.Message);

            return InternalServerError(list);
        }
    }

    /// <summary>
    /// A Service Result with a success object and an error object.
    /// </summary>
    /// <typeparam name="TSuccess">Success object</typeparam>
    /// <typeparam name="TError">Error object</typeparam>
    public record ServiceResult<TSuccess, TError> : ServiceResult<TSuccess>
    {
        public TError? ErrorValue { get; init; }

        public bool HasPartiallySucceeded => StatusCode == ServiceResultStatus.PartialSuccess;

        /// <summary>
        /// Creates a successful <see cref="ServiceResult{TSuccess, TError}"/> instance with the specified success
        /// value.
        /// </summary>
        /// <param name="successValue">The value representing the successful result. Defaults to <see langword="default"/> if not specified.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.Success"/>
        /// and the specified success value.</returns>
        public static new ServiceResult<TSuccess?, TError> Succeed(TSuccess? successValue = default) => new()
        {
            StatusCode = ServiceResultStatus.Success,
            SuccessValue = successValue
        };

        public static new ServiceResult<TSuccess?, TError?> SuccessNoContent() => new()
        {
            StatusCode = ServiceResultStatus.SuccessNoContent
        };

        /// <summary>
        /// Creates a service result that represents a partial success, containing both a success value and an error value.
        /// </summary>
        /// <param name="successValue">The value representing the successful part of the operation. Can be <see langword="null"/> if no success value is available.</param>
        /// <param name="errorValue">The value representing the error or failure part of the operation.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.PartialSuccess"/>, containing the provided success and error values.</returns>
        public static ServiceResult<TSuccess?, TError> PartialSuccess(TSuccess? successValue, TError errorValue) => new()
        {
            StatusCode = ServiceResultStatus.PartialSuccess,
            SuccessValue = successValue,
            ErrorValue = errorValue
        };

        /// <summary>
        /// Creates a failed result with the specified error message.
        /// </summary>
        /// <param name="error">The error message describing the failure. Cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> representing a failed operation with the specified error message.</returns>
        public static new ServiceResult<TSuccess, TError> Fail(string error) => Fail([error]);

        /// <summary>
        /// Creates a failed <see cref="ServiceResult{TSuccess, TError}"/> with the specified error messages.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the failure. Cannot be null.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with a status of  <see cref="ServiceResultStatus.GenericError"/> and the provided error messages.</returns>
        public static new ServiceResult<TSuccess, TError> Fail(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.GenericError,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a <see cref="ServiceResult{TSuccess, TError}"/> instance representing a forbidden operation.
        /// </summary>
        /// <param name="error">A string describing the error or reason for the forbidden operation. Cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with a status indicating the operation is forbidden and the provided error message.</returns>
        public static new ServiceResult<TSuccess, TError> Forbidden(string error) => Forbidden([error]);

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance with a status of <see
        /// cref="ServiceResultStatus.Forbidden"/>  and the specified collection of error messages.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the reason for the forbidden status. Cannot be null.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.Forbidden"/> and the provided error messages.</returns>
        public static new ServiceResult<TSuccess, TError> Forbidden(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Forbidden,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance representing a "Not Found" result.
        /// </summary>
        /// <param name="error">A message describing the error or reason for the "Not Found" result. Cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with the "Not Found" status and the specified error message.</returns>
        public static new ServiceResult<TSuccess, TError> NotFound(string error) => NotFound([error]);

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance with a status of <see
        /// cref="ServiceResultStatus.NotFound"/>.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the reason for the "Not Found" status.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.NotFound"/> and the specified error messages.</returns>
        public static new ServiceResult<TSuccess, TError> NotFound(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.NotFound,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance representing a conflict error.
        /// </summary>
        /// <param name="error">The error message describing the conflict. Cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with a conflict status and the specified error message.</returns>
        public static new ServiceResult<TSuccess, TError> Conflict(string error) => Conflict([error]);

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance with a status of <see
        /// cref="ServiceResultStatus.Conflict"/>  and the specified error messages.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the conflict. Cannot be null.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.Conflict"/>  and the provided error messages.</returns>
        public static new ServiceResult<TSuccess, TError> Conflict(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Conflict,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance representing a "Gone" result.
        /// </summary>
        /// <param name="error">The error message describing the reason for the "Gone" result.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with the specified error message.</returns>
        public static new ServiceResult<TSuccess, TError> Gone(string error) => Gone([error]);

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance with a status of <see
        /// cref="ServiceResultStatus.Gone"/>  and the specified error messages.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the reason for the "Gone" status. Cannot be null.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status of <see cref="ServiceResultStatus.Gone"/>  and the provided error messages.</returns>
        public static new ServiceResult<TSuccess, TError> Gone(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.Gone,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance representing an internal server error.
        /// </summary>
        /// <param name="error">A string describing the error that occurred. This value cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance with an error state indicating an internal server error.</returns>
        public static new ServiceResult<TSuccess, TError> InternalServerError(string error) => InternalServerError([error]);

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance representing an internal server error.
        /// </summary>
        /// <param name="errors">A collection of error messages describing the internal server error.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> with a status code of <see cref="ServiceResultStatus.InternalServerError"/> and the specified error messages.</returns>
        public static new ServiceResult<TSuccess, TError> InternalServerError(IEnumerable<string> errors) => new()
        {
            StatusCode = ServiceResultStatus.InternalServerError,
            Errors = [.. errors]
        };

        /// <summary>
        /// Creates a service result representing an internal server error with the specified exception details and
        /// context.
        /// </summary>
        /// <param name="ex">The exception that caused the internal server error. Cannot be <see langword="null"/>.</param>
        /// <param name="context">A string providing additional context about the error. Cannot be <see langword="null"/> or empty.</param>
        /// <returns>A <see cref="ServiceResult{TSuccess, TError}"/> instance representing the internal server error, containing the exception message, context, and any inner exception messages.</returns>
        public static new ServiceResult<TSuccess, TError> InternalServerError(Exception ex, string context)
        {
            var list = new List<string> { context, ex.Message };
            if (ex.InnerException != null)
                list.Add(ex.InnerException.Message);

            return InternalServerError(list);
        }

        /// <summary>
        /// Creates a new <see cref="ServiceResult{TSuccess, TError}"/> instance based on an existing  <see
        /// cref="ServiceResult{TSuccess}"/> and an optional error value.
        /// </summary>
        /// <param name="baseResult">The base result containing the status code, errors, and success value to copy.</param>
        /// <param name="errorValue">An optional error value to associate with the new result. Defaults to <see langword="default"/> if not provided.</param>
        /// <returns>A new <see cref="ServiceResult{TSuccess, TError}"/> instance with the values from <paramref name="baseResult"/>  and the specified <paramref name="errorValue"/>.</returns>
        public static ServiceResult<TSuccess, TError> FromBase(ServiceResult<TSuccess> baseResult, TError? errorValue = default) => new()
        {
            StatusCode = baseResult.StatusCode,
            Errors = baseResult.Errors,
            SuccessValue = baseResult.SuccessValue,
            ErrorValue = errorValue
        };
    }

    public readonly record struct Empty;

    public static class ServiceResult
    {
        public static ServiceResult<Empty> Succeed() => ServiceResult<Empty>.SuccessNoContent();
        public static ServiceResult<Empty> Fail(IEnumerable<string> errors) => ServiceResult<Empty>.Fail(errors);
        public static ServiceResult<Empty> Fail(string error) => ServiceResult<Empty>.Fail(error);
    }
}