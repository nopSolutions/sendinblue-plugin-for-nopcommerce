using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Misc.SendInBlue
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Misc.SendInBlue.ImportUsers",
                "Plugins/SendInBlue/ImportUsers",
                new { controller = "SendInBlue", action = "ImportUsers" },
                new[] { "Nop.Plugin.Misc.SendInBlue.Controllers" });

            routes.MapRoute("Plugin.Misc.SendInBlue.Unsubscribe",
                "Plugins/SendInBlue/UnsubscribeWebHook",
                new { controller = "SendInBlue", action = "UnsubscribeWebHook" },
                new[] { "Nop.Plugin.Misc.SendInBlue.Controllers" });
        }

        public int Priority
        {
            get { return 0; }
        }

    }
}