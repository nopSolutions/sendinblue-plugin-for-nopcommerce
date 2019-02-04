using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.SendInBlue.Infrastructure
{
    /// <summary>
    /// Represents a SendInBlue route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute(SendInBlueDefaults.ImportContactsRoute,
                "Plugins/SendInBlue/ImportContacts",
                new { controller = "SendInBlue", action = "ImportContacts" });

            routeBuilder.MapRoute(SendInBlueDefaults.UnsubscribeContactRoute,
                "Plugins/SendInBlue/UnsubscribeWebHook",
                new { controller = "SendInBlue", action = "UnsubscribeWebHook" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}