namespace AscendantEmpire.Application.Common.Validation;

/// <summary>
/// A single validation failure — the human-readable message a rule produced when it rejected a value.
/// </summary>
/// <remarks>
/// <para>Deliberately carries nothing but the message. The API's error contract (<c>IApiErrorResult</c>) is a flat array of strings and the React client
/// joins that array into one message, so a property name, error code or severity would have nowhere to go.</para>
/// </remarks>
/// <param name="ErrorMessage">The message to show the user.</param>
public record ValidationFailure(string ErrorMessage);
