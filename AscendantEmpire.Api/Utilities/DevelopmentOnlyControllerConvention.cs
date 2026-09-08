using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AscendantEmpire.Api.Utilities;

// Remove in F-18. Added to keep the developer end-turn endpoint out of every environment but Development. Nothing else uses it, so the whole file goes
// with that controller.

/// <summary>
/// Marks a controller that must only exist when the API is running in the Development environment.
/// </summary>
/// <remarks>
/// <para>Remove in F-18. Applied to controllers that do something no deployed environment should offer at all — as opposed to something a caller needs a
/// permission for. <see cref="DevelopmentOnlyControllerConvention"/> is what enforces it.</para>
/// <para>The attribute on its own does nothing. It is inert unless the convention is registered.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DevelopmentOnlyAttribute : Attribute
{
}

/// <summary>
/// Drops every controller marked <see cref="DevelopmentOnlyAttribute"/> from the application model.
/// </summary>
/// <remarks>
/// <para>Remove in F-18. Registered only when the environment is not Development, so the marked controllers are built normally while developing and are
/// absent everywhere else.</para>
/// <para>Removing the controller from the model rather than rejecting its requests means the routes never exist: a call answers 404 the way any unknown
/// path does, and the endpoint is not discoverable through Swagger or a 405 on the wrong verb.</para>
/// </remarks>
public class DevelopmentOnlyControllerConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Backwards, because removing an entry shifts everything after it.
        for (int controllerIndex = application.Controllers.Count - 1; controllerIndex >= 0; controllerIndex--)
        {
            ControllerModel controller = application.Controllers[controllerIndex];

            if (controller.Attributes.OfType<DevelopmentOnlyAttribute>().Any())
            {
                application.Controllers.RemoveAt(controllerIndex);
            }
        }
    }
}
