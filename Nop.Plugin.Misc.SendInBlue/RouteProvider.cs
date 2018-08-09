using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.SendInBlue
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.Misc.SendInBlue.ImportUsers",
                "Plugins/SendInBlue/ImportUsers",
                new { controller = "SendInBlue", action = "ImportUsers" });

            routeBuilder.MapRoute("Plugin.Misc.SendInBlue.Unsubscribe",
                "Plugins/SendInBlue/UnsubscribeWebHook",
                new { controller = "SendInBlue", action = "UnsubscribeWebHook" });
        }
        

        public int Priority => 0;
    }
}